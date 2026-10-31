using System;
using System.Drawing;
using System.Windows.Forms;

namespace bitAudio.Controls
{
    internal sealed class ProfileNameDialog : Form
    {
        private static readonly Color BackgroundColor = Color.FromArgb(31, 31, 40);
        private static readonly Color TextColor = Color.FromArgb(237, 238, 243);
        private static readonly Color InputColor = Color.FromArgb(45, 45, 57);
        private static readonly Color AccentColor = Color.FromArgb(90, 209, 200);

        private readonly TextBox nameBox;

        public ProfileNameDialog(string suggestedName)
        {
            Text = "Guardar perfil";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(390, 142);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9F);

            Label label = new Label();
            label.Text = "Nombre del perfil";
            label.AutoSize = true;
            label.ForeColor = TextColor;
            label.Location = new Point(18, 18);
            Controls.Add(label);

            nameBox = new TextBox();
            nameBox.Location = new Point(18, 44);
            nameBox.Size = new Size(354, 24);
            nameBox.BackColor = InputColor;
            nameBox.ForeColor = TextColor;
            nameBox.BorderStyle = BorderStyle.FixedSingle;
            nameBox.MaxLength = 40;
            nameBox.Text = suggestedName ?? string.Empty;
            Controls.Add(nameBox);

            Button okButton = new Button();
            okButton.Text = "Guardar";
            okButton.Location = new Point(190, 92);
            okButton.Size = new Size(88, 30);
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = AccentColor;
            okButton.BackColor = AccentColor;
            okButton.ForeColor = Color.FromArgb(16, 34, 34);
            okButton.Font = new Font("Segoe UI Semibold", 9F);
            okButton.Cursor = Cursors.Hand;
            okButton.UseVisualStyleBackColor = false;
            okButton.Click += HandleOkClick;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancelar";
            cancelButton.Location = new Point(284, 92);
            cancelButton.Size = new Size(88, 30);
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 86);
            cancelButton.BackColor = InputColor;
            cancelButton.ForeColor = TextColor;
            cancelButton.Font = new Font("Segoe UI Semibold", 9F);
            cancelButton.Cursor = Cursors.Hand;
            cancelButton.UseVisualStyleBackColor = false;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Shown += delegate
            {
                nameBox.Focus();
                nameBox.SelectAll();
            };
        }

        public string ProfileName { get; private set; }

        private void HandleOkClick(object sender, EventArgs e)
        {
            string name = (nameBox.Text ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Escribe un nombre para el perfil.", "Guardar perfil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameBox.Focus();
                return;
            }

            ProfileName = name;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
