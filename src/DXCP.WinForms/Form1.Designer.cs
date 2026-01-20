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
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            barManager = new DevExpress.XtraBars.BarManager(components);
            barDockControlTop = new DevExpress.XtraBars.BarDockControl();
            barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
            barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
            barDockControlRight = new DevExpress.XtraBars.BarDockControl();
            barButtonCherryPick = new DevExpress.XtraBars.BarButtonItem();
            popupMenuGrid = new DevExpress.XtraBars.PopupMenu(components);
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
            ((System.ComponentModel.ISupportInitialize)barManager).BeginInit();
            ((System.ComponentModel.ISupportInitialize)popupMenuGrid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridControl).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridView).BeginInit();
            SuspendLayout();
            //
            // barManager
            //
            barManager.DockControls.Add(barDockControlTop);
            barManager.DockControls.Add(barDockControlBottom);
            barManager.DockControls.Add(barDockControlLeft);
            barManager.DockControls.Add(barDockControlRight);
            barManager.Form = this;
            barManager.Items.AddRange(new DevExpress.XtraBars.BarItem[] { barButtonCherryPick });
            barManager.MaxItemId = 1;
            //
            // barButtonCherryPick
            //
            barButtonCherryPick.Caption = "Cherry Pick";
            barButtonCherryPick.Id = 0;
            barButtonCherryPick.Name = "barButtonCherryPick";
            barButtonCherryPick.ItemClick += barButtonCherryPick_ItemClick;
            //
            // popupMenuGrid
            //
            popupMenuGrid.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] {
                new DevExpress.XtraBars.LinkPersistInfo(barButtonCherryPick)
            });
            popupMenuGrid.Manager = barManager;
            popupMenuGrid.Name = "popupMenuGrid";
            //
            // barDockControlTop
            //
            barDockControlTop.CausesValidation = false;
            barDockControlTop.Dock = DockStyle.Top;
            barDockControlTop.Location = new Point(0, 0);
            barDockControlTop.Manager = barManager;
            barDockControlTop.Size = new Size(984, 0);
            //
            // barDockControlBottom
            //
            barDockControlBottom.CausesValidation = false;
            barDockControlBottom.Dock = DockStyle.Bottom;
            barDockControlBottom.Location = new Point(0, 526);
            barDockControlBottom.Manager = barManager;
            barDockControlBottom.Size = new Size(984, 0);
            //
            // barDockControlLeft
            //
            barDockControlLeft.CausesValidation = false;
            barDockControlLeft.Dock = DockStyle.Left;
            barDockControlLeft.Location = new Point(0, 0);
            barDockControlLeft.Manager = barManager;
            barDockControlLeft.Size = new Size(0, 526);
            //
            // barDockControlRight
            //
            barDockControlRight.CausesValidation = false;
            barDockControlRight.Dock = DockStyle.Right;
            barDockControlRight.Location = new Point(984, 0);
            barDockControlRight.Manager = barManager;
            barDockControlRight.Size = new Size(0, 526);
            //
            // gridControl
            // 
            gridControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            gridControl.Location = new Point(12, 47);
            gridControl.MainView = gridView;
            gridControl.Name = "gridControl";
            gridControl.Size = new Size(960, 468);
            gridControl.TabIndex = 0;
            gridControl.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { gridView });
            // 
            // gridView
            // 
            gridView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { colNumber, colTitle, colState, colRepository, colCreatedAt, colBaseBranch, colHeadBranch, colIsDraft, colAdditions, colDeletions });
            gridView.DetailHeight = 328;
            gridView.GridControl = gridControl;
            gridView.Name = "gridView";
            gridView.OptionsBehavior.Editable = false;
            gridView.OptionsView.BestFitMode = DevExpress.XtraGrid.Views.Grid.GridBestFitMode.Fast;
            gridView.OptionsView.ColumnAutoWidth = false;
            gridView.OptionsView.ShowGroupPanel = false;
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
            colCreatedAt.DisplayFormat.FormatString = "g";
            colCreatedAt.DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            colCreatedAt.FieldName = "CreatedAt";
            colCreatedAt.Name = "colCreatedAt";
            colCreatedAt.Visible = true;
            colCreatedAt.VisibleIndex = 4;
            colCreatedAt.Width = 120;
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
            btnRefresh.ImageOptions.SvgImage = Properties.Resources.changeview;
            btnRefresh.Location = new Point(12, 11);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(100, 26);
            btnRefresh.TabIndex = 1;
            btnRefresh.Text = "Refresh";
            btnRefresh.Click += btnRefresh_Click;
            //
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(984, 526);
            Controls.Add(btnRefresh);
            Controls.Add(gridControl);
            Controls.Add(barDockControlLeft);
            Controls.Add(barDockControlRight);
            Controls.Add(barDockControlBottom);
            Controls.Add(barDockControlTop);
            Name = "Form1";
            Text = "My Pull Requests";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)barManager).EndInit();
            ((System.ComponentModel.ISupportInitialize)popupMenuGrid).EndInit();
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
        private DevExpress.XtraBars.BarManager barManager;
        private DevExpress.XtraBars.BarDockControl barDockControlTop;
        private DevExpress.XtraBars.BarDockControl barDockControlBottom;
        private DevExpress.XtraBars.BarDockControl barDockControlLeft;
        private DevExpress.XtraBars.BarDockControl barDockControlRight;
        private DevExpress.XtraBars.BarButtonItem barButtonCherryPick;
        private DevExpress.XtraBars.PopupMenu popupMenuGrid;
    }
}
