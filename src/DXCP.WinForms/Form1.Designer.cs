namespace DXCP.WinForms
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
            components = new System.ComponentModel.Container();
            gridControl = new DevExpress.XtraGrid.GridControl();
            gridView = new DevExpress.XtraGrid.Views.Grid.GridView();
            colNumber = new DevExpress.XtraGrid.Columns.GridColumn();
            colTitle = new DevExpress.XtraGrid.Columns.GridColumn();
            colState = new DevExpress.XtraGrid.Columns.GridColumn();
            colRepository = new DevExpress.XtraGrid.Columns.GridColumn();
            colCreatedAt = new DevExpress.XtraGrid.Columns.GridColumn();
            colBaseBranch = new DevExpress.XtraGrid.Columns.GridColumn();
            colHeadBranch = new DevExpress.XtraGrid.Columns.GridColumn();
            colIsDraft = new DevExpress.XtraGrid.Columns.GridColumn();
            colAdditions = new DevExpress.XtraGrid.Columns.GridColumn();
            colDeletions = new DevExpress.XtraGrid.Columns.GridColumn();
            btnRefresh = new DevExpress.XtraEditors.SimpleButton();
            labelStatus = new DevExpress.XtraEditors.LabelControl();
            ((System.ComponentModel.ISupportInitialize)gridControl).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridView).BeginInit();
            SuspendLayout();
            //
            // gridControl
            //
            gridControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            gridControl.Location = new Point(12, 50);
            gridControl.MainView = gridView;
            gridControl.Name = "gridControl";
            gridControl.Size = new Size(960, 499);
            gridControl.TabIndex = 0;
            gridControl.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { gridView });
            //
            // gridView
            //
            gridView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
                colNumber,
                colTitle,
                colState,
                colRepository,
                colCreatedAt,
                colBaseBranch,
                colHeadBranch,
                colIsDraft,
                colAdditions,
                colDeletions
            });
            gridView.GridControl = gridControl;
            gridView.Name = "gridView";
            gridView.OptionsBehavior.Editable = false;
            gridView.OptionsView.ShowGroupPanel = false;
            gridView.OptionsView.ColumnAutoWidth = false;
            gridView.OptionsView.BestFitMode = DevExpress.XtraGrid.Views.Grid.GridBestFitMode.Fast;
            //
            // colNumber
            //
            colNumber.Caption = "#";
            colNumber.FieldName = "Number";
            colNumber.Name = "colNumber";
            colNumber.Visible = true;
            colNumber.VisibleIndex = 0;
            colNumber.Width = 50;
            //
            // colTitle
            //
            colTitle.Caption = "Title";
            colTitle.FieldName = "Title";
            colTitle.Name = "colTitle";
            colTitle.Visible = true;
            colTitle.VisibleIndex = 1;
            colTitle.Width = 300;
            //
            // colState
            //
            colState.Caption = "State";
            colState.FieldName = "State";
            colState.Name = "colState";
            colState.Visible = true;
            colState.VisibleIndex = 2;
            colState.Width = 70;
            //
            // colRepository
            //
            colRepository.Caption = "Repository";
            colRepository.FieldName = "Repository";
            colRepository.Name = "colRepository";
            colRepository.Visible = true;
            colRepository.VisibleIndex = 3;
            colRepository.Width = 180;
            //
            // colCreatedAt
            //
            colCreatedAt.Caption = "Created";
            colCreatedAt.FieldName = "CreatedAt";
            colCreatedAt.Name = "colCreatedAt";
            colCreatedAt.Visible = true;
            colCreatedAt.VisibleIndex = 4;
            colCreatedAt.Width = 120;
            colCreatedAt.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            colCreatedAt.DisplayFormat.FormatString = "g";
            //
            // colBaseBranch
            //
            colBaseBranch.Caption = "Base Branch";
            colBaseBranch.FieldName = "BaseBranch";
            colBaseBranch.Name = "colBaseBranch";
            colBaseBranch.Visible = true;
            colBaseBranch.VisibleIndex = 5;
            colBaseBranch.Width = 100;
            //
            // colHeadBranch
            //
            colHeadBranch.Caption = "Head Branch";
            colHeadBranch.FieldName = "HeadBranch";
            colHeadBranch.Name = "colHeadBranch";
            colHeadBranch.Visible = true;
            colHeadBranch.VisibleIndex = 6;
            colHeadBranch.Width = 100;
            //
            // colIsDraft
            //
            colIsDraft.Caption = "Draft";
            colIsDraft.FieldName = "IsDraft";
            colIsDraft.Name = "colIsDraft";
            colIsDraft.Visible = true;
            colIsDraft.VisibleIndex = 7;
            colIsDraft.Width = 50;
            //
            // colAdditions
            //
            colAdditions.Caption = "+";
            colAdditions.FieldName = "Additions";
            colAdditions.Name = "colAdditions";
            colAdditions.Visible = true;
            colAdditions.VisibleIndex = 8;
            colAdditions.Width = 50;
            //
            // colDeletions
            //
            colDeletions.Caption = "-";
            colDeletions.FieldName = "Deletions";
            colDeletions.Name = "colDeletions";
            colDeletions.Visible = true;
            colDeletions.VisibleIndex = 9;
            colDeletions.Width = 50;
            //
            // btnRefresh
            //
            btnRefresh.Location = new Point(12, 12);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(100, 28);
            btnRefresh.TabIndex = 1;
            btnRefresh.Text = "Refresh";
            btnRefresh.Click += btnRefresh_Click;
            //
            // labelStatus
            //
            labelStatus.Location = new Point(130, 18);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(0, 16);
            labelStatus.TabIndex = 2;
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(7F, 16F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(984, 561);
            Controls.Add(labelStatus);
            Controls.Add(btnRefresh);
            Controls.Add(gridControl);
            Name = "Form1";
            Text = "My Pull Requests";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)gridControl).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridView).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DevExpress.XtraGrid.GridControl gridControl;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView;
        private DevExpress.XtraGrid.Columns.GridColumn colNumber;
        private DevExpress.XtraGrid.Columns.GridColumn colTitle;
        private DevExpress.XtraGrid.Columns.GridColumn colState;
        private DevExpress.XtraGrid.Columns.GridColumn colRepository;
        private DevExpress.XtraGrid.Columns.GridColumn colCreatedAt;
        private DevExpress.XtraGrid.Columns.GridColumn colBaseBranch;
        private DevExpress.XtraGrid.Columns.GridColumn colHeadBranch;
        private DevExpress.XtraGrid.Columns.GridColumn colIsDraft;
        private DevExpress.XtraGrid.Columns.GridColumn colAdditions;
        private DevExpress.XtraGrid.Columns.GridColumn colDeletions;
        private DevExpress.XtraEditors.SimpleButton btnRefresh;
        private DevExpress.XtraEditors.LabelControl labelStatus;
    }
}
