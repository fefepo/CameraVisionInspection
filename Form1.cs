using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// ✅ 타입 충돌 방지 별칭
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;

namespace CameraVisionInspection
{
    public partial class Form1 : Form
    {
        // ===== 영상 재생 =====
        private VideoCapture? _cap;
        private CancellationTokenSource? _cts;
        private Task? _worker;
        private bool _isRunning = false;

        // ===== 처리 파라미터 =====
        private Mat? _lastFrame;                    // 최신 프레임(ROI 좌표 변환용)
        private volatile bool _useOtsu = true;
        private volatile int _manualThresh = 128;

        // ===== Morphology 파라미터 =====
        private volatile bool _useMorph = true;
        private volatile int _openK = 3;   // Open 커널 (노이즈 제거)
        private volatile int _closeK = 5;  // Close 커널 (끊김 연결)

        // ===== 결함(컨투어) 파라미터 =====
        private volatile int _minDefectArea = 50;      // 개별 결함 최소 면적(노이즈 컷)
        private volatile int _ngDefectCount = 1;       // 결함 개수 기준(>=이면 NG)
        private double _ngTotalArea = 500.0;  // 결함 면적 합 기준(>=이면 NG)

        // ===== 저장/이력 =====
        private readonly string _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private readonly string _csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "inspection_log.csv");
        private volatile bool _saveNgOnly = true;      // NG만 저장
        private volatile int _saveCooldownMs = 800;    // 연속 저장 방지(0.8초)
        private long _lastSaveTick = 0;

        // ===== ROI =====
        private bool _isRoiDragging = false;
        private DrawingPoint _roiStartPt;
        private Rectangle _roiRectPb;               // PictureBox 기준 ROI
        private CvRect? _roiRectImg = null;         // Mat 기준 ROI

        // ===== 저장 이미지 열기 =====
        private readonly object _logLock = new object();
        private const int _maxLogRows = 300;   // 너무 많아지면 UI 느려져서 제한


        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // UI 초기화
            checkBoxOtsu.Checked = true;

            trackBarThresh.Minimum = 0;
            trackBarThresh.Maximum = 255;
            trackBarThresh.Value = 128;

            _useOtsu = true;
            _manualThresh = trackBarThresh.Value;
            trackBarThresh.Enabled = !checkBoxOtsu.Checked;

            // ROI 이벤트 연결
            pictureBoxCam.MouseDown += pictureBoxCam_MouseDown;
            pictureBoxCam.MouseMove += pictureBoxCam_MouseMove;
            pictureBoxCam.MouseUp += pictureBoxCam_MouseUp;
            pictureBoxCam.Paint += pictureBoxCam_Paint;

            // ✅ 로그 폴더/CSV 준비
            Directory.CreateDirectory(_logDir);
            if (!File.Exists(_csvPath))
            {
                File.WriteAllText(_csvPath, "Time,Judge,DefectCount,TotalArea,ImagePath\n");
            }

            // ===== 리포트(ListView) 초기화 =====
            listViewLog.View = View.Details;
            listViewLog.FullRowSelect = true;
            listViewLog.GridLines = true;

            if (listViewLog.Columns.Count == 0)
            {
                listViewLog.Columns.Add("Time", 160);
                listViewLog.Columns.Add("Judge", 60);
                listViewLog.Columns.Add("Count", 60);
                listViewLog.Columns.Add("Area", 80);
                listViewLog.Columns.Add("ImagePath", 420);
            }

            listViewLog.DoubleClick += listViewLog_DoubleClick;

            // (선택) Clear 버튼
            if (Controls.Find("buttonClearLog", true).Length > 0)
            {
                buttonClearLog.Click += (s, ev) => listViewLog.Items.Clear();
            }

            buttonOpenCsv.Click += (s, ev) => OpenFileIfExists(_csvPath);
            buttonOpenFolder.Click += (s, ev) => OpenFolderIfExists(_logDir);

            StopCamera();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopCamera();
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (_isRunning) return;
            StopCamera();

            string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_inspection.mp4");
            if (!File.Exists(videoPath))
            {
                MessageBox.Show($"영상 파일을 찾을 수 없습니다.\n경로: {videoPath}\n\n" +
                                "mp4 추가 후 'Copy to Output Directory' = Copy if newer 설정하세요.");
                return;
            }

            _cap = new VideoCapture(videoPath);
            if (!_cap.IsOpened())
            {
                _cap.Dispose();
                _cap = null;
                MessageBox.Show("영상 파일을 열 수 없습니다.");
                return;
            }

            _cts = new CancellationTokenSource();
            _isRunning = true;

            _worker = Task.Run(() => VideoLoop(_cts.Token));
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            StopCamera();
        }

        private void StopCamera()
        {
            try { _cts?.Cancel(); } catch { }

            try { _worker?.Wait(500); } catch { }
            _worker = null;

            _cts?.Dispose();
            _cts = null;

            if (_cap != null)
            {
                try { _cap.Release(); } catch { }
                _cap.Dispose();
                _cap = null;
            }

            _lastFrame?.Dispose();
            _lastFrame = null;

            _isRunning = false;
        }

        private void VideoLoop(CancellationToken token)
        {
            using var frame = new Mat();

            while (!token.IsCancellationRequested)
            {
                bool ok = _cap != null && _cap.Read(frame);
                if (!ok || frame.Empty())
                {
                    _cap?.Set(VideoCaptureProperties.PosFrames, 0);
                    Thread.Sleep(10);
                    continue;
                }

                // 최신 프레임 저장(ROI 좌표 변환용)
                _lastFrame?.Dispose();
                _lastFrame = frame.Clone();

                // 1) Gray
                using var gray = new Mat();
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

                // 2) Threshold
                using var bin = new Mat();
                if (_useOtsu)
                    Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                else
                    Cv2.Threshold(gray, bin, _manualThresh, 255, ThresholdTypes.Binary);

                // 3) Morphology (Open/Close)
                using var bin2 = new Mat();
                if (_useMorph)
                {
                    // 커널은 홀수 권장 (3,5,7...)
                    int okk = (_openK < 1) ? 1 : (_openK % 2 == 0 ? _openK + 1 : _openK);
                    int ckk = (_closeK < 1) ? 1 : (_closeK % 2 == 0 ? _closeK + 1 : _closeK);

                    using var kernelOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(okk, okk));
                    using var kernelClose = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(ckk, ckk));

                    using var tmp = new Mat();
                    Cv2.MorphologyEx(bin, tmp, MorphTypes.Open, kernelOpen);     // 작은 점 노이즈 제거
                    Cv2.MorphologyEx(tmp, bin2, MorphTypes.Close, kernelClose);  // 끊어진 결함 연결
                }
                else
                {
                    bin.CopyTo(bin2);
                }

                // ===== 표시용(색 박스 그리려면 BGR이어야 함) =====
                using var overlay = new Mat();
                Cv2.CvtColor(bin2, overlay, ColorConversionCodes.GRAY2BGR);

                int defectCount = 0;
                double totalArea = 0;

                // ✅ ROI를 아직 안 그렸으면 "검사 안 함"
                if (!_roiRectImg.HasValue)
                {
                    Cv2.PutText(overlay, "Draw ROI to inspect",
                        new CvPoint(10, 30), HersheyFonts.HersheySimplex, 0.9, Scalar.Yellow, 2);

                    var bmp0 = BitmapConverter.ToBitmap(overlay);

                    try
                    {
                        pictureBoxCam.Invoke(new Action(() =>
                        {
                            pictureBoxCam.Image?.Dispose();
                            pictureBoxCam.Image = bmp0;

                            if (Controls.Find("labelDefect", true).Length > 0)
                            {
                                var lbl = Controls.Find("labelDefect", true)[0] as Label;
                                if (lbl != null) lbl.Text = $"Defects: {defectCount}";
                            }
                        }));
                    }
                    catch
                    {
                        bmp0.Dispose();
                        break;
                    }

                    Thread.Sleep(10);
                    continue;
                }

                // ===== 결함 검출: ROI 안에서만 (Morphology 적용된 bin2 기준) =====
                CvRect detectRect = _roiRectImg.Value;
                using var binRoi = new Mat(bin2, detectRect);

                Cv2.FindContours(
                    binRoi,
                    out CvPoint[][] contours,
                    out HierarchyIndex[] hierarchy,
                    RetrievalModes.External,
                    ContourApproximationModes.ApproxSimple
                );

                foreach (var c in contours)
                {
                    double area = Cv2.ContourArea(c);
                    if (area < _minDefectArea) continue; // 작은 노이즈 제거

                    totalArea += area;

                    // ROI 좌표계 → 전체 좌표계 보정
                    var r = Cv2.BoundingRect(c);
                    r.X += detectRect.X;
                    r.Y += detectRect.Y;

                    Cv2.Rectangle(overlay, r, Scalar.Red, 2);
                    defectCount++;
                }

                // ===== 판정(OK/NG) =====
                bool isNg = (defectCount >= _ngDefectCount) || (totalArea >= _ngTotalArea);
                string judge = isNg ? "NG" : "OK";
                Scalar judgeColor = isNg ? Scalar.Red : Scalar.LimeGreen;


                // 화면 텍스트 표시
                Cv2.PutText(overlay, $"Defects: {defectCount}  Area: {(int)totalArea}",
                    new CvPoint(10, 30), HersheyFonts.HersheySimplex, 0.9, Scalar.Yellow, 2);

                Cv2.PutText(overlay, judge,
                    new CvPoint(10, 75), HersheyFonts.HersheySimplex, 2.0, judgeColor, 4);

                // ===== 결과 저장(이미지 + CSV) =====
                bool shouldSave = (!_saveNgOnly) || isNg;
                long nowTick = Environment.TickCount64;

                if (shouldSave && (nowTick - _lastSaveTick >= _saveCooldownMs))
                {
                    _lastSaveTick = nowTick;

                    string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    string imgName = $"{ts}_{judge}_C{defectCount}_A{(int)totalArea}.png";
                    string imgPath = Path.Combine(_logDir, imgName);

                    // overlay(박스/판정 포함) 저장
                    Cv2.ImWrite(imgPath, overlay);

                    // CSV 로그 append
                    string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{judge},{defectCount},{(int)totalArea},{imgPath}\n";
                    try { File.AppendAllText(_csvPath, line); } catch { /* 파일 잠김 등 예외 무시 */ }


                    // ✅ UI 리포트(ListView)에 기록 추가
                    AddLogRow(DateTime.Now, judge, defectCount, (int)totalArea, imgPath);
                }

                // Mat → Bitmap → UI
                var bmp = BitmapConverter.ToBitmap(overlay);

                try
                {
                    pictureBoxCam.Invoke(new Action(() =>
                    {
                        pictureBoxCam.Image?.Dispose();
                        pictureBoxCam.Image = bmp;

                        if (Controls.Find("labelDefect", true).Length > 0)
                        {
                            var lbl = Controls.Find("labelDefect", true)[0] as Label;
                            if (lbl != null) lbl.Text = $"Defects: {defectCount} | {judge}";
                        }
                    }));
                }
                catch
                {
                    bmp.Dispose();
                    break;
                }

                Thread.Sleep(10);
            }
        }

        // ===== ROI 드래그 =====
        private void pictureBoxCam_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (_lastFrame == null || _lastFrame.Empty()) return;

            _isRoiDragging = true;
            _roiStartPt = e.Location;
            _roiRectPb = new Rectangle(e.Location, DrawingSize.Empty);
            pictureBoxCam.Invalidate();
        }

        private void pictureBoxCam_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRoiDragging) return;

            int x1 = Math.Min(_roiStartPt.X, e.X);
            int y1 = Math.Min(_roiStartPt.Y, e.Y);
            int x2 = Math.Max(_roiStartPt.X, e.X);
            int y2 = Math.Max(_roiStartPt.Y, e.Y);

            _roiRectPb = Rectangle.FromLTRB(x1, y1, x2, y2);
            pictureBoxCam.Invalidate();
        }

        private void pictureBoxCam_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isRoiDragging) return;
            _isRoiDragging = false;

            if (_roiRectPb.Width < 5 || _roiRectPb.Height < 5)
            {
                _roiRectImg = null;
                pictureBoxCam.Invalidate();
                return;
            }

            _roiRectImg = PbRectToImageRect(_roiRectPb, pictureBoxCam, _lastFrame!);
            pictureBoxCam.Invalidate();
        }

        private void pictureBoxCam_Paint(object sender, PaintEventArgs e)
        {
            if (_roiRectPb.Width > 0 && _roiRectPb.Height > 0)
            {
                using var pen = new Pen(Color.Lime, 2);
                e.Graphics.DrawRectangle(pen, _roiRectPb);
            }
        }

        // PictureBox(Zoom) 좌표 → Mat 좌표
        private CvRect? PbRectToImageRect(Rectangle pbRect, PictureBox pb, Mat img)
        {
            Rectangle imgOnPb = GetImageDisplayRectangle(pb, img.Width, img.Height);
            Rectangle inter = Rectangle.Intersect(pbRect, imgOnPb);
            if (inter.Width <= 0 || inter.Height <= 0) return null;

            double sx = img.Width / (double)imgOnPb.Width;
            double sy = img.Height / (double)imgOnPb.Height;

            int x = (int)((inter.X - imgOnPb.X) * sx);
            int y = (int)((inter.Y - imgOnPb.Y) * sy);
            int w = (int)(inter.Width * sx);
            int h = (int)(inter.Height * sy);

            x = Math.Max(0, Math.Min(x, img.Width - 1));
            y = Math.Max(0, Math.Min(y, img.Height - 1));
            w = Math.Max(1, Math.Min(w, img.Width - x));
            h = Math.Max(1, Math.Min(h, img.Height - y));

            return new CvRect(x, y, w, h);
        }

        // Zoom 모드에서 실제 이미지가 그려지는 영역 계산(레터박스 포함)
        private Rectangle GetImageDisplayRectangle(PictureBox pb, int imgW, int imgH)
        {
            float pbW = pb.ClientSize.Width;
            float pbH = pb.ClientSize.Height;

            float imgAsp = imgW / (float)imgH;
            float pbAsp = pbW / pbH;

            int w, h;
            if (imgAsp > pbAsp)
            {
                w = (int)pbW;
                h = (int)(pbW / imgAsp);
            }
            else
            {
                h = (int)pbH;
                w = (int)(pbH * imgAsp);
            }

            int x = (int)((pbW - w) / 2);
            int y = (int)((pbH - h) / 2);
            return new Rectangle(x, y, w, h);
        }

        // ===== UI 이벤트 =====
        private void trackBarThresh_Scroll(object sender, EventArgs e)
        {
            _manualThresh = trackBarThresh.Value;
        }

        private void checkBoxOtsu_CheckedChanged(object sender, EventArgs e)
        {
            _useOtsu = checkBoxOtsu.Checked;
            trackBarThresh.Enabled = !_useOtsu;
        }

        // 디자이너에 Click 이벤트가 걸려있을 수 있어서 빈 핸들러 유지
        private void pictureBoxCam_Click(object sender, EventArgs e)
        {
            // 사용 안 함
        }

        private void AddLogRow(DateTime t, string judge, int count, int area, string imgPath)
        {
            // VideoLoop는 백그라운드 스레드라 Invoke 필요
            if (listViewLog.IsDisposed) return;

            try
            {
                listViewLog.BeginInvoke(new Action(() =>
                {
                    lock (_logLock)
                    {
                        var item = new ListViewItem(t.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        item.SubItems.Add(judge);
                        item.SubItems.Add(count.ToString());
                        item.SubItems.Add(area.ToString());
                        item.SubItems.Add(imgPath);

                        // 경로는 Tag에도 저장(더블클릭에서 사용)
                        item.Tag = imgPath;

                        // 맨 위에 최신 기록이 오게
                        listViewLog.Items.Insert(0, item);

                        // 너무 많아지면 오래된 것 제거
                        while (listViewLog.Items.Count > _maxLogRows)
                            listViewLog.Items.RemoveAt(listViewLog.Items.Count - 1);
                    }
                }));
            }
            catch
            {
                // 폼 닫히는 중이면 무시
            }
        }

        private void listViewLog_DoubleClick(object? sender, EventArgs e)
        {
            if (listViewLog.SelectedItems.Count == 0) return;

            var item = listViewLog.SelectedItems[0];
            var path = item.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;

            if (!File.Exists(path))
            {
                MessageBox.Show("이미지 파일이 존재하지 않습니다:\n" + path);
                return;
            }

            try
            {
                // 기본 이미지 뷰어로 열기
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("이미지를 열 수 없습니다:\n" + ex.Message);
            }
        }

        private void OpenFileIfExists(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show("파일이 없습니다:\n" + path);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일 열기 실패:\n" + ex.Message);
            }
        }

        private void OpenFolderIfExists(string dir)
        {
            if (!Directory.Exists(dir))
            {
                MessageBox.Show("폴더가 없습니다:\n" + dir);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("폴더 열기 실패:\n" + ex.Message);
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {

        }
    }
}
