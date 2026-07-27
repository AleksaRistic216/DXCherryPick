using DevExpress.XtraEditors;

namespace DXCP.WinForms;

public class ErrorDialog : XtraForm
{
    private MemoEdit memoDetails = null!;
    private SimpleButton btnCopy = null!;
    private SimpleButton btnClose = null!;

    public ErrorDialog(string message, Exception? exception = null)
    {
        InitializeComponents();
        Text = "Error";
        Icon = SystemIcons.Error;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(message);
        if (exception != null)
        {
            sb.AppendLine();
            sb.Append(exception.ToString());
        }
        memoDetails.Text = sb.ToString();
    }

    private void InitializeComponents()
    {
        memoDetails = new MemoEdit();
        btnCopy = new SimpleButton();
        btnClose = new SimpleButton();

        ((System.ComponentModel.ISupportInitialize)memoDetails.Properties).BeginInit();
        SuspendLayout();

        memoDetails.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        memoDetails.Location = new Point(12, 12);
        memoDetails.Size = new Size(660, 340);
        memoDetails.Properties.ReadOnly = true;
        memoDetails.Properties.ScrollBars = ScrollBars.Both;
        memoDetails.Properties.WordWrap = false;
        memoDetails.Font = new Font("Consolas", 9f);

        btnCopy.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnCopy.Location = new Point(492, 364);
        btnCopy.Size = new Size(90, 26);
        btnCopy.Text = "Copy All";
        btnCopy.Click += (_, _) => Clipboard.SetText(memoDetails.Text);

        btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClose.Location = new Point(594, 364);
        btnClose.Size = new Size(78, 26);
        btnClose.Text = "Close";
        btnClose.Click += (_, _) => Close();

        AutoScaleDimensions = new SizeF(7f, 15f);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(684, 402);
        MinimumSize = new Size(500, 300);
        Controls.Add(memoDetails);
        Controls.Add(btnCopy);
        Controls.Add(btnClose);
        StartPosition = FormStartPosition.CenterParent;

        ((System.ComponentModel.ISupportInitialize)memoDetails.Properties).EndInit();
        ResumeLayout(false);
    }

    public static void Show(IWin32Window owner, string message, Exception? exception = null)
    {
        using var dlg = new ErrorDialog(message, exception);
        dlg.ShowDialog(owner);
    }
}
