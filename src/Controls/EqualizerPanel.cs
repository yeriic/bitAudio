using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace bitAudio.Controls
{
    internal sealed class EqualizerPanel : UserControl
    {
        public const int PanelHeight = 194;

        private static readonly Color EqBackColor = Color.FromArgb(35, 35, 46);
        private static readonly Color TextColor = Color.FromArgb(237, 238, 243);
        private static readonly Color MutedColor = Color.FromArgb(156, 157, 171);
        private static readonly Color AccentColor = Color.FromArgb(90, 209, 200);
        private static readonly Color WarningColor = Color.FromArgb(242, 185, 96);
        private static readonly Color InputColor = Color.FromArgb(45, 45, 57);
        private static readonly Color ButtonBorderColor = Color.FromArgb(70, 70, 86);
        private static readonly Color ComboBackColor = Color.FromArgb(246, 247, 250);
        private static readonly Color ComboTextColor = Color.FromArgb(27, 28, 37);
        private static readonly Color ComboSelectionColor = Color.FromArgb(199, 238, 233);
        private static readonly Color SeparatorColor = Color.FromArgb(55, 55, 68);

        private readonly List<EqProfile> builtInProfiles = new List<EqProfile>();
        private readonly List<EqProfile> customProfiles = new List<EqProfile>();
        private readonly float[] gains = new float[EqualizerSampleProvider.BandCount];
        private readonly float[] personalGains = new float[EqualizerSampleProvider.BandCount];
        private readonly VerticalEqSlider[] sliders;
        private readonly Label[] gainLabels;
        private readonly Label[] frequencyLabels;
        private readonly Panel[] bandPanels;
        private readonly EqProfile personalProfile;
        private readonly ComboBox profileCombo;
        private readonly Button saveButton;
        private readonly Panel bandsHost;
        private readonly Timer saveTimer;

        private string activeProfileName = "Plano";
        private bool suppressSliderEvents;
        private bool suppressProfileEvents;
        private bool stateDirty;

        public event EventHandler GainsChanged;
        public event Action<string> StatusMessage;

        public EqualizerPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            BackColor = EqBackColor;
            Font = new Font("Segoe UI", 9F);
            Size = new Size(820, PanelHeight);

            sliders = new VerticalEqSlider[EqualizerSampleProvider.BandCount];
            gainLabels = new Label[EqualizerSampleProvider.BandCount];
            frequencyLabels = new Label[EqualizerSampleProvider.BandCount];
            bandPanels = new Panel[EqualizerSampleProvider.BandCount];
            personalProfile = new EqProfile(EqProfileStore.PersonalProfileName, personalGains, false);

            Panel topLine = new Panel();
            topLine.Location = new Point(0, 0);
            topLine.Size = new Size(Width, 1);
            topLine.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            topLine.BackColor = SeparatorColor;
            Controls.Add(topLine);

            Label title = new Label();
            title.Text = "Ecualizador";
            title.Font = new Font("Segoe UI Semibold", 9.5F);
            title.ForeColor = TextColor;
            title.AutoSize = true;
            title.Location = new Point(0, 11);
            Controls.Add(title);

            profileCombo = CreateCombo(90, 6, 190);
            profileCombo.SelectedIndexChanged += HandleProfileChanged;
            Controls.Add(profileCombo);

            saveButton = CreateButton("Guardar", 292, 4, 100, 27);
            saveButton.Click += HandleSaveClick;
            Controls.Add(saveButton);

            bandsHost = new Panel();
            bandsHost.Location = new Point(0, 38);
            bandsHost.Size = new Size(Width, 146);
            bandsHost.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            bandsHost.BackColor = EqBackColor;
            bandsHost.Resize += delegate { LayoutBands(); };
            Controls.Add(bandsHost);

            BuildBands();
            LoadProfilesAndState();
            LayoutBands();

            saveTimer = new Timer();
            saveTimer.Interval = 500;
            saveTimer.Tick += HandleSaveTimer;
        }

        public float[] Gains
        {
            get { return EqProfile.CopyGains(gains); }
        }

        public void SaveState()
        {
            if (saveTimer != null)
            {
                saveTimer.Stop();
            }
            PersistState();
        }

        private void BuildBands()
        {
            string[] frequencies = { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };

            for (int i = 0; i < EqualizerSampleProvider.BandCount; i++)
            {
                Panel bandPanel = new Panel();
                bandPanel.BackColor = EqBackColor;
                bandsHost.Controls.Add(bandPanel);
                bandPanels[i] = bandPanel;

                Label gainLabel = new Label();
                gainLabel.AutoSize = false;
                gainLabel.TextAlign = ContentAlignment.MiddleCenter;
                gainLabel.Font = new Font("Segoe UI Semibold", 8.5F);
                gainLabel.ForeColor = MutedColor;
                gainLabel.BackColor = Color.Transparent;
                gainLabel.Text = "0.0";
                bandPanel.Controls.Add(gainLabel);
                gainLabels[i] = gainLabel;

                VerticalEqSlider slider = new VerticalEqSlider();
                slider.Minimum = (int)(EqualizerSampleProvider.MinimumGainDb * 10f);
                slider.Maximum = (int)(EqualizerSampleProvider.MaximumGainDb * 10f);
                slider.SmallChange = 5;
                slider.LargeChange = 20;
                slider.Value = 0;
                slider.BackColor = EqBackColor;
                slider.AccentColor = AccentColor;
                slider.CutColor = WarningColor;
                slider.TrackColor = Color.FromArgb(65, 65, 80);
                slider.ThumbColor = Color.FromArgb(242, 243, 248);
                int bandIndex = i;
                slider.ValueChanged += delegate
                {
                    HandleBandChanged(bandIndex);
                };
                bandPanel.Controls.Add(slider);
                sliders[i] = slider;

                Label frequencyLabel = new Label();
                frequencyLabel.AutoSize = false;
                frequencyLabel.TextAlign = ContentAlignment.MiddleCenter;
                frequencyLabel.Font = new Font("Segoe UI", 8F);
                frequencyLabel.ForeColor = MutedColor;
                frequencyLabel.BackColor = Color.Transparent;
                frequencyLabel.Text = frequencies[i];
                bandPanel.Controls.Add(frequencyLabel);
                frequencyLabels[i] = frequencyLabel;
            }
        }

        private void LayoutBands()
        {
            if (bandsHost == null || bandPanels == null)
            {
                return;
            }

            int totalWidth = bandsHost.ClientSize.Width;
            int bandWidth = totalWidth / EqualizerSampleProvider.BandCount;
            if (bandWidth < 1)
            {
                return;
            }

            int left = 0;
            for (int i = 0; i < EqualizerSampleProvider.BandCount; i++)
            {
                int width = (i == EqualizerSampleProvider.BandCount - 1)
                    ? totalWidth - left
                    : bandWidth;

                Panel bandPanel = bandPanels[i];
                bandPanel.Bounds = new Rectangle(left, 0, width, bandsHost.ClientSize.Height);

                Label gainLabel = gainLabels[i];
                gainLabel.Bounds = new Rectangle(0, 0, width, 16);

                VerticalEqSlider slider = sliders[i];
                slider.Location = new Point((width - slider.Width) / 2, 18);

                Label frequencyLabel = frequencyLabels[i];
                frequencyLabel.Bounds = new Rectangle(0, 112, width, 16);

                left += bandWidth;
            }
        }

        private void LoadProfilesAndState()
        {
            builtInProfiles.Clear();
            builtInProfiles.AddRange(EqProfileStore.GetBuiltInProfiles());

            customProfiles.Clear();
            customProfiles.AddRange(EqProfileStore.LoadCustomProfiles());

            RebuildProfileCombo("Plano");

            EqState state = EqProfileStore.LoadState();
            float[] initialGains = null;
            string initialProfile = "Plano";

            if (state != null)
            {
                initialGains = state.Gains;
                initialProfile = state.ActiveProfileName;
            }

            if (initialGains == null)
            {
                EqProfile flat = FindProfile("Plano");
                initialGains = flat != null ? flat.Gains : new float[EqualizerSampleProvider.BandCount];
            }

            Array.Copy(EqProfile.CopyGains(initialGains), gains, gains.Length);
            Array.Copy(gains, personalGains, gains.Length);

            EqProfile selected = FindProfile(initialProfile);
            if (string.Equals(initialProfile, EqProfileStore.PersonalProfileName, StringComparison.OrdinalIgnoreCase))
            {
                selected = personalProfile;
            }
            else if (selected == null)
            {
                selected = personalProfile;
                initialProfile = EqProfileStore.PersonalProfileName;
            }
            else if (!GainsEqual(initialGains, selected.Gains))
            {
                selected = personalProfile;
                initialProfile = EqProfileStore.PersonalProfileName;
            }

            personalProfile.Gains = EqProfile.CopyGains(personalGains);
            ApplyGainsToUi(gains);
            activeProfileName = initialProfile;

            suppressProfileEvents = true;
            profileCombo.SelectedItem = selected;
            if (profileCombo.SelectedItem == null && selected != null)
            {
                profileCombo.SelectedItem = personalProfile;
            }
            suppressProfileEvents = false;
        }

        private void RebuildProfileCombo(string preferredName)
        {
            suppressProfileEvents = true;
            try
            {
                profileCombo.Items.Clear();

                EqProfile flat = FindProfile("Plano");
                if (flat != null)
                {
                    profileCombo.Items.Add(flat);
                }

                profileCombo.Items.Add(personalProfile);

                foreach (EqProfile profile in builtInProfiles)
                {
                    if (!string.Equals(profile.Name, "Plano", StringComparison.OrdinalIgnoreCase))
                    {
                        profileCombo.Items.Add(profile);
                    }
                }

                foreach (EqProfile profile in customProfiles)
                {
                    profileCombo.Items.Add(profile);
                }

                EqProfile preferred = FindProfile(preferredName);
                if (string.Equals(preferredName, EqProfileStore.PersonalProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    preferred = personalProfile;
                }
                if (preferred != null)
                {
                    profileCombo.SelectedItem = preferred;
                }
                else
                {
                    profileCombo.SelectedIndex = 0;
                }
            }
            finally
            {
                suppressProfileEvents = false;
            }
        }

        private EqProfile FindProfile(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (EqProfile profile in builtInProfiles)
            {
                if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            foreach (EqProfile profile in customProfiles)
            {
                if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            return null;
        }

        private static bool GainsEqual(float[] left, float[] right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            int count = Math.Min(left.Length, right.Length);
            for (int i = 0; i < count; i++)
            {
                if (Math.Abs(left[i] - right[i]) > 0.05f)
                {
                    return false;
                }
            }
            return left.Length == right.Length;
        }

        private void ApplyGainsToUi(float[] source)
        {
            float[] copy = EqProfile.CopyGains(source);
            Array.Copy(copy, gains, gains.Length);

            suppressSliderEvents = true;
            try
            {
                for (int i = 0; i < gains.Length; i++)
                {
                    sliders[i].Value = (int)Math.Round(gains[i] * 10f);
                    UpdateBandLabel(i);
                }
            }
            finally
            {
                suppressSliderEvents = false;
            }
        }

        private void UpdateBandLabel(int index)
        {
            float gain = gains[index];
            Label label = gainLabels[index];
            label.Text = gain.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture);
            if (gain > 0.05f)
            {
                label.ForeColor = AccentColor;
            }
            else if (gain < -0.05f)
            {
                label.ForeColor = WarningColor;
            }
            else
            {
                label.ForeColor = MutedColor;
            }
        }

        private void HandleBandChanged(int index)
        {
            if (suppressSliderEvents)
            {
                return;
            }

            gains[index] = EqProfile.ClampGain(sliders[index].Value / 10f);
            UpdateBandLabel(index);

            if (!string.Equals(activeProfileName, EqProfileStore.PersonalProfileName, StringComparison.OrdinalIgnoreCase))
            {
                activeProfileName = EqProfileStore.PersonalProfileName;
                Array.Copy(gains, personalGains, gains.Length);
                personalProfile.Gains = EqProfile.CopyGains(personalGains);

                suppressProfileEvents = true;
                profileCombo.SelectedItem = personalProfile;
                suppressProfileEvents = false;
            }
            else
            {
                Array.Copy(gains, personalGains, gains.Length);
                personalProfile.Gains = EqProfile.CopyGains(personalGains);
            }

            RaiseGainsChanged();
            MarkStateDirty();
        }

        private void HandleProfileChanged(object sender, EventArgs e)
        {
            if (suppressProfileEvents)
            {
                return;
            }

            EqProfile selected = profileCombo.SelectedItem as EqProfile;
            if (selected == null)
            {
                return;
            }

            activeProfileName = selected.Name;
            float[] target = selected == personalProfile ? personalGains : selected.Gains;
            ApplyGainsToUi(target);
            RaiseGainsChanged();
            MarkStateDirty();
        }

        private void HandleSaveClick(object sender, EventArgs e)
        {
            string suggestedName = string.Empty;
            if (!string.Equals(activeProfileName, EqProfileStore.PersonalProfileName, StringComparison.OrdinalIgnoreCase))
            {
                EqProfile current = FindProfile(activeProfileName);
                if (current != null && !current.IsBuiltIn)
                {
                    suggestedName = activeProfileName;
                }
            }

            using (ProfileNameDialog dialog = new ProfileNameDialog(suggestedName))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                string name = dialog.ProfileName.Trim();
                if (EqProfileStore.IsReservedName(name))
                {
                    MessageBox.Show(
                        FindForm(),
                        "Ese nombre está reservado. Elige otro.",
                        "Guardar perfil",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                EqProfile existing = FindCustomProfile(name);
                if (existing != null)
                {
                    DialogResult overwrite = MessageBox.Show(
                        FindForm(),
                        "Ya existe un perfil con ese nombre. ¿Sobrescribirlo?",
                        "Guardar perfil",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (overwrite != DialogResult.Yes)
                    {
                        return;
                    }
                }

                try
                {
                    EqProfile saved = EqProfileStore.SaveCustomProfile(name, gains);
                    if (existing != null)
                    {
                        existing.Gains = EqProfile.CopyGains(saved.Gains);
                        saved = existing;
                    }
                    else
                    {
                        customProfiles.Add(saved);
                    }

                    customProfiles.Sort(delegate(EqProfile left, EqProfile right)
                    {
                        return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
                    });

                    activeProfileName = saved.Name;
                    RebuildProfileCombo(saved.Name);
                    personalProfile.Gains = EqProfile.CopyGains(personalGains);
                    MarkStateDirty();
                    SaveState();
                    RaiseStatusMessage("Perfil guardado: " + saved.Name);
                }
                catch (Exception exception)
                {
                    RaiseStatusMessage("No se pudo guardar el perfil: " + exception.Message);
                }
            }
        }

        private EqProfile FindCustomProfile(string name)
        {
            foreach (EqProfile profile in customProfiles)
            {
                if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }
            return null;
        }

        private void RaiseGainsChanged()
        {
            EventHandler handler = GainsChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseStatusMessage(string message)
        {
            Action<string> handler = StatusMessage;
            if (handler != null)
            {
                handler(message);
            }
        }

        private void MarkStateDirty()
        {
            stateDirty = true;
            if (saveTimer != null)
            {
                saveTimer.Stop();
                saveTimer.Start();
            }
        }

        private void HandleSaveTimer(object sender, EventArgs e)
        {
            saveTimer.Stop();
            if (stateDirty)
            {
                PersistState();
            }
        }

        private void PersistState()
        {
            try
            {
                EqProfileStore.SaveState(activeProfileName, gains);
                stateDirty = false;
            }
            catch (Exception exception)
            {
                RaiseStatusMessage("No se pudo guardar el ecualizador: " + exception.Message);
            }
        }

        private static ComboBox CreateCombo(int x, int y, int width)
        {
            ComboBox combo = new ComboBox();
            combo.Location = new Point(x, y);
            combo.Width = width;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Standard;
            combo.BackColor = ComboBackColor;
            combo.ForeColor = ComboTextColor;
            combo.Font = new Font("Segoe UI", 9F);
            combo.IntegralHeight = false;
            combo.MaxDropDownItems = 10;
            combo.DropDownWidth = Math.Max(width, 240);
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 22;
            combo.DrawItem += HandleComboDrawItem;
            return combo;
        }

        private static Button CreateButton(string text, int x, int y, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = ButtonBorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 72);
            button.BackColor = InputColor;
            button.ForeColor = TextColor;
            button.Font = new Font("Segoe UI Semibold", 8.5F);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private static void HandleComboDrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo == null)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selected ? ComboSelectionColor : ComboBackColor;

            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            if (e.Index >= 0 && e.Index < combo.Items.Count)
            {
                string text = combo.Items[e.Index] != null ? combo.Items[e.Index].ToString() : string.Empty;
                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    combo.Font,
                    e.Bounds,
                    ComboTextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
            {
                e.DrawFocusRectangle();
            }
        }
    }
}
