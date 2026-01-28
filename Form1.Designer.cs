namespace CameraVisionInspection
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pictureBoxCam = new PictureBox();
            buttonStart = new Button();
            buttonStop = new Button();
            checkBoxOtsu = new CheckBox();
            trackBarThresh = new TrackBar();
            listViewLog = new ListView();
            buttonClearLog = new Button();
            buttonOpenCsv = new Button();
            buttonOpenFolder = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBoxCam).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackBarThresh).BeginInit();
            SuspendLayout();
            // 
            // pictureBoxCam
            // 
            pictureBoxCam.Location = new Point(177, 89);
            pictureBoxCam.Name = "pictureBoxCam";
            pictureBoxCam.Size = new Size(260, 149);
            pictureBoxCam.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxCam.TabIndex = 0;
            pictureBoxCam.TabStop = false;
            pictureBoxCam.Click += pictureBoxCam_Click;
            // 
            // buttonStart
            // 
            buttonStart.Location = new Point(190, 268);
            buttonStart.Name = "buttonStart";
            buttonStart.Size = new Size(92, 33);
            buttonStart.TabIndex = 1;
            buttonStart.Text = "시작";
            buttonStart.UseVisualStyleBackColor = true;
            buttonStart.Click += buttonStart_Click;
            // 
            // buttonStop
            // 
            buttonStop.Location = new Point(330, 268);
            buttonStop.Name = "buttonStop";
            buttonStop.Size = new Size(92, 33);
            buttonStop.TabIndex = 2;
            buttonStop.Text = "멈춤\r\n";
            buttonStop.UseVisualStyleBackColor = true;
            buttonStop.Click += buttonStop_Click;
            // 
            // checkBoxOtsu
            // 
            checkBoxOtsu.AutoSize = true;
            checkBoxOtsu.Location = new Point(177, 55);
            checkBoxOtsu.Name = "checkBoxOtsu";
            checkBoxOtsu.Size = new Size(56, 19);
            checkBoxOtsu.TabIndex = 3;
            checkBoxOtsu.Text = "OTSU";
            checkBoxOtsu.UseVisualStyleBackColor = true;
            checkBoxOtsu.CheckedChanged += checkBoxOtsu_CheckedChanged;
            // 
            // trackBarThresh
            // 
            trackBarThresh.Location = new Point(261, 321);
            trackBarThresh.Name = "trackBarThresh";
            trackBarThresh.Size = new Size(104, 45);
            trackBarThresh.TabIndex = 5;
            trackBarThresh.Scroll += trackBarThresh_Scroll;
            // 
            // listViewLog
            // 
            listViewLog.FullRowSelect = true;
            listViewLog.GridLines = true;
            listViewLog.Location = new Point(505, 80);
            listViewLog.Name = "listViewLog";
            listViewLog.Size = new Size(216, 333);
            listViewLog.TabIndex = 6;
            listViewLog.UseCompatibleStateImageBehavior = false;
            listViewLog.View = View.Details;
            // 
            // buttonClearLog
            // 
            buttonClearLog.Location = new Point(505, 51);
            buttonClearLog.Name = "buttonClearLog";
            buttonClearLog.Size = new Size(75, 23);
            buttonClearLog.TabIndex = 7;
            buttonClearLog.Text = "Clear Log";
            buttonClearLog.UseVisualStyleBackColor = true;
            // 
            // buttonOpenCsv
            // 
            buttonOpenCsv.Location = new Point(592, 419);
            buttonOpenCsv.Name = "buttonOpenCsv";
            buttonOpenCsv.Size = new Size(75, 23);
            buttonOpenCsv.TabIndex = 8;
            buttonOpenCsv.Text = "CSV 열기";
            buttonOpenCsv.UseVisualStyleBackColor = true;
            // 
            // buttonOpenFolder
            // 
            buttonOpenFolder.Location = new Point(505, 419);
            buttonOpenFolder.Name = "buttonOpenFolder";
            buttonOpenFolder.Size = new Size(75, 23);
            buttonOpenFolder.TabIndex = 9;
            buttonOpenFolder.Text = "폴더 열기";
            buttonOpenFolder.UseVisualStyleBackColor = true;
            buttonOpenFolder.Click += button2_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(buttonOpenFolder);
            Controls.Add(buttonOpenCsv);
            Controls.Add(buttonClearLog);
            Controls.Add(listViewLog);
            Controls.Add(trackBarThresh);
            Controls.Add(checkBoxOtsu);
            Controls.Add(buttonStop);
            Controls.Add(buttonStart);
            Controls.Add(pictureBoxCam);
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBoxCam).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackBarThresh).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox pictureBoxCam;
        private Button buttonStart;
        private Button buttonStop;
        private CheckBox checkBoxOtsu;
        private TrackBar trackBarThresh;
        private ListView listViewLog;
        private Button buttonClearLog;
        private Button buttonOpenCsv;
        private Button buttonOpenFolder;
    }
}
