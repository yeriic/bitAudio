using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using bitAudio.Controls;

namespace bitAudio
{
    internal sealed class MainForm : Form
    {
        private static readonly Color BackgroundColor = Color.FromArgb(19, 19, 24);
        private static readonly Color CardColor = Color.FromArgb(31, 31, 40);
        private static readonly Color CardHoverColor = Color.FromArgb(38, 38, 49);
        private static readonly Color InputColor = Color.FromArgb(45, 45, 57);
        private static readonly Color TextColor = Color.FromArgb(237, 238, 243);
        private static readonly Color MutedColor = Color.FromArgb(156, 157, 171);
        private static readonly Color AccentColor = Color.FromArgb(90, 209, 200);
        private static readonly Color SuccessColor = Color.FromArgb(111, 219, 144);
        private static readonly Color WarningColor = Color.FromArgb(242, 185, 96);
        private static readonly Color ErrorColor = Color.FromArgb(255, 112, 112);
        private static readonly Color ButtonBorderColor = Color.FromArgb(70, 70, 86);
        private static readonly Color ComboBackColor = Color.FromArgb(246, 247, 250);
        private static readonly Color ComboTextColor = Color.FromArgb(27, 28, 37);
        private static readonly Color ComboSelectionColor = Color.FromArgb(199, 238, 233);

        private readonly AudioEngine engine;
        private readonly Timer syncTimer;

        private Panel salidaPanel;
        private Panel entradaPanel;
        private Panel settingsPanel;
        private Button tabEntradaButton;
        private Button tabSalidaButton;
        private Button tabAjustesButton;
        private ComboBox inputCombo;
        private ComboBox destinationCombo;
        private ComboBox outputCombo;
        private SlimSlider outputVolumeSlider;
        private SlimSlider gainSlider;
        private Label outputVolumeValue;
        private Label gainValueLabel;
        private CheckBox muteCheck;
        private CheckBox limiterCheck;
        private CheckBox autoStartCheck;
        private CheckBox startMinimizedCheck;
        private Label statusLabel;
        private LinkLabel cableLink;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private Button eqButton;
        private EqualizerPanel eqPanel;
        private Panel eqShiftSeparator;
        private Label eqShiftTitle;
        private Timer eqAnimationTimer;
        private Stopwatch eqAnimationWatch;
        private bool eqOpen;
        private int eqClosedClientHeight;
        private int eqExtraHeight;
        private int eqAnimationStartHeight;
        private int eqAnimationEndHeight;
        private int eqAnimationStartAmpTop;
        private int eqAnimationEndAmpTop;
        private int eqAnimationStartFormHeight;
        private int eqAnimationEndFormHeight;
        private const int EqTop = 184;
        private Panel noiseSeparator;
        private Label noiseTitle;
        private Label noiseGateValueLabel;
        private Label noiseLevelLabel;
        private NoiseGateBar noiseGateBar;
        private Timer meterTimer;
        private bool suppressNoiseGateEvents;

        private List<AudioDeviceInfo> inputDevices = new List<AudioDeviceInfo>();
        private List<AudioDeviceInfo> outputDevices = new List<AudioDeviceInfo>();
        private List<AudioDeviceInfo> destinationDevices = new List<AudioDeviceInfo>();

        private bool suppressUiEvents;
        private bool suppressSettingsEvents;
        private bool restarting;
        private bool closing;
        private bool allowExit;
        private bool hidingToTray;
        private bool trayBalloonShown;
        private bool launchedFromStartup;
        private bool startInBackground;
        private int hintTick;

        public MainForm()
        {
            engine = new AudioEngine();
            syncTimer = new Timer();

            StartupManager.MigrateLegacySettings(Application.ExecutablePath);
            launchedFromStartup = HasCommandLineArgument("--startup");
            startInBackground = launchedFromStartup && StartupManager.GetStartMinimized();

            Text = "bitAudio";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 560);
            MinimumSize = new Size(880, 600);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Font;
            DoubleBuffered = true;

            BuildUi();
            CreateTrayIcon();

            int savedGain = StartupManager.GetGainPercent();
            suppressUiEvents = true;
            gainSlider.Value = savedGain;
            suppressUiEvents = false;
            engine.Gain = savedGain / 100f;
            UpdateGainDisplay();

            engine.SetEqualizerGains(eqPanel.Gains);
            eqPanel.GainsChanged += HandleEqGainsChanged;
            eqPanel.StatusMessage += HandleEqStatusMessage;
            eqPanel.MonitoringToggleRequested += HandleMonitoringToggleRequested;

            eqAnimationTimer = new Timer();
            eqAnimationTimer.Interval = 15;
            eqAnimationTimer.Tick += HandleEqAnimationTick;
            eqAnimationWatch = new Stopwatch();

            int savedNoiseGate = StartupManager.GetNoiseGateThresholdDb();
            suppressNoiseGateEvents = true;
            noiseGateBar.ValueDb = savedNoiseGate;
            suppressNoiseGateEvents = false;
            engine.SetNoiseGateThreshold(savedNoiseGate);
            noiseGateValueLabel.Text = "Umbral " + savedNoiseGate + " dB";

            meterTimer = new Timer();
            meterTimer.Interval = 50;
            meterTimer.Tick += HandleMeterTimer;

            engine.StatusChanged += HandleEngineStatus;
            engine.ErrorOccurred += HandleEngineError;

            syncTimer.Interval = 250;
            syncTimer.Tick += HandleSyncTimer;

            FormClosing += HandleFormClosing;
            Shown += HandleShown;
            Resize += HandleResize;

            if (startInBackground)
            {
                ShowInTaskbar = false;
                WindowState = FormWindowState.Minimized;
            }
        }

        private static bool HasCommandLineArgument(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (string current in args)
            {
                if (string.Equals(current, argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.BackColor = BackgroundColor;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = BackgroundColor;
            header.Margin = Padding.Empty;
            root.Controls.Add(header, 0, 0);

            Label title = new Label();
            title.Text = "bitAudio";
            title.Font = new Font("Segoe UI Semibold", 22F);
            title.ForeColor = AccentColor;
            title.AutoSize = true;
            title.Location = new Point(20, 10);
            header.Controls.Add(title);

            Panel tabBar = new Panel();
            tabBar.Dock = DockStyle.Fill;
            tabBar.BackColor = BackgroundColor;
            tabBar.Margin = Padding.Empty;
            root.Controls.Add(tabBar, 0, 1);

            tabEntradaButton = CreateTabButton("Entrada", 20, 2);
            tabEntradaButton.Click += delegate { SelectTab(0); };
            tabBar.Controls.Add(tabEntradaButton);

            tabSalidaButton = CreateTabButton("Salida", 132, 2);
            tabSalidaButton.Click += delegate { SelectTab(1); };
            tabBar.Controls.Add(tabSalidaButton);

            tabAjustesButton = CreateTabButton("Ajustes", 244, 2);
            tabAjustesButton.Click += delegate { SelectTab(2); };
            tabBar.Controls.Add(tabAjustesButton);

            LinkLabel helpLink = new LinkLabel();
            helpLink.Text = "Ayuda";
            helpLink.AutoSize = true;
            helpLink.Location = new Point(820, 14);
            helpLink.LinkColor = MutedColor;
            helpLink.ActiveLinkColor = TextColor;
            helpLink.VisitedLinkColor = MutedColor;
            helpLink.LinkBehavior = LinkBehavior.HoverUnderline;
            helpLink.BackColor = BackgroundColor;
            helpLink.LinkClicked += HandleHelpClick;
            tabBar.Controls.Add(helpLink);
            tabBar.Resize += delegate
            {
                helpLink.Location = new Point(tabBar.ClientSize.Width - helpLink.Width - 20, 14);
            };

            Panel contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = CardColor;
            contentHost.Margin = new Padding(20, 0, 20, 10);
            root.Controls.Add(contentHost, 0, 2);

            salidaPanel = BuildSalidaPanel();
            entradaPanel = BuildEntradaPanel();
            settingsPanel = BuildSettingsPanel();
            contentHost.Controls.Add(entradaPanel);
            contentHost.Controls.Add(salidaPanel);
            contentHost.Controls.Add(settingsPanel);

            Panel statusBar = new Panel();
            statusBar.Dock = DockStyle.Fill;
            statusBar.BackColor = BackgroundColor;
            statusBar.Margin = new Padding(20, 0, 20, 6);
            root.Controls.Add(statusBar, 0, 3);

            statusLabel = new Label();
            statusLabel.Text = "Iniciando…";
            statusLabel.Location = new Point(0, 8);
            statusLabel.Size = new Size(840, 20);
            statusLabel.ForeColor = MutedColor;
            statusLabel.Font = new Font("Segoe UI", 9F);
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusBar.Controls.Add(statusLabel);

            statusBar.Resize += delegate
            {
                statusLabel.Width = statusBar.ClientSize.Width;
            };

            SelectTab(0);
        }

        private Panel BuildSalidaPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = CardColor;

            Label sectionOut = CreateSectionLabel("Salida de Windows", 20, 16);
            panel.Controls.Add(sectionOut);

            outputCombo = CreateCombo(20, 42, 820);
            outputCombo.SelectedIndexChanged += HandleOutputChanged;
            panel.Controls.Add(outputCombo);

            Panel separator = CreateSeparator(20, 90, 820);
            panel.Controls.Add(separator);

            Label volumeTitle = CreateSectionLabel("Volumen", 20, 102);
            panel.Controls.Add(volumeTitle);

            outputVolumeValue = new Label();
            outputVolumeValue.Text = "—";
            outputVolumeValue.Font = new Font("Segoe UI Semibold", 10.5F);
            outputVolumeValue.ForeColor = AccentColor;
            outputVolumeValue.AutoSize = true;
            outputVolumeValue.Location = new Point(110, 102);
            panel.Controls.Add(outputVolumeValue);

            outputVolumeSlider = CreateSlider(20, 132, 820, 0, 100, 1, 5);
            outputVolumeSlider.Value = 100;
            outputVolumeSlider.ValueChanged += HandleOutputVolumeChanged;
            panel.Controls.Add(outputVolumeSlider);

            muteCheck = new CheckBox();
            muteCheck.Text = "Silenciar";
            muteCheck.Appearance = Appearance.Button;
            muteCheck.FlatStyle = FlatStyle.Flat;
            muteCheck.FlatAppearance.BorderColor = ButtonBorderColor;
            muteCheck.FlatAppearance.CheckedBackColor = Color.FromArgb(92, 45, 52);
            muteCheck.FlatAppearance.MouseOverBackColor = CardHoverColor;
            muteCheck.BackColor = InputColor;
            muteCheck.ForeColor = TextColor;
            muteCheck.TextAlign = ContentAlignment.MiddleCenter;
            muteCheck.Size = new Size(120, 30);
            muteCheck.Location = new Point(20, 174);
            muteCheck.CheckedChanged += HandleMuteChanged;
            panel.Controls.Add(muteCheck);

            Label note = CreateCaption("Solo controla el volumen de Windows de esta salida.", 20, 220);
            panel.Controls.Add(note);

            panel.Resize += delegate
            {
                int width = panel.ClientSize.Width - 40;
                if (width < 300)
                {
                    width = 300;
                }
                outputCombo.Width = width;
                outputVolumeSlider.Width = width;
                separator.Width = width;
            };

            return panel;
        }

        private Panel BuildEntradaPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = CardColor;

            Label micTitle = CreateSectionLabel("Micrófono", 20, 16);
            panel.Controls.Add(micTitle);

            inputCombo = CreateCombo(20, 42, 820);
            inputCombo.SelectedIndexChanged += HandleInputChanged;
            panel.Controls.Add(inputCombo);

            Label destinationTitle = CreateSectionLabel("Destino (cable virtual)", 20, 80);
            panel.Controls.Add(destinationTitle);

            destinationCombo = CreateCombo(20, 106, 820);
            destinationCombo.SelectedIndexChanged += HandleDestinationChanged;
            panel.Controls.Add(destinationCombo);

            Button refreshButton = CreateButton("Actualizar", 20, 142, 100, 30);
            refreshButton.Click += HandleRefreshClick;
            panel.Controls.Add(refreshButton);

            Button soundButton = CreateButton("Abrir Sonido de Windows", 128, 142, 190, 30);
            soundButton.Click += HandleSoundClick;
            panel.Controls.Add(soundButton);

            eqButton = CreateButton("EQ", 330, 142, 70, 30);
            eqButton.Click += delegate { ToggleEqualizer(); };
            panel.Controls.Add(eqButton);

            cableLink = new LinkLabel();
            cableLink.Text = "Instalar VB-CABLE";
            cableLink.AutoSize = true;
            cableLink.Location = new Point(414, 148);
            cableLink.LinkColor = AccentColor;
            cableLink.ActiveLinkColor = TextColor;
            cableLink.VisitedLinkColor = AccentColor;
            cableLink.LinkBehavior = LinkBehavior.HoverUnderline;
            cableLink.BackColor = CardColor;
            cableLink.Visible = false;
            cableLink.LinkClicked += HandleCableLinkClicked;
            panel.Controls.Add(cableLink);

            Panel separator = CreateSeparator(20, EqTop, 820);
            panel.Controls.Add(separator);
            eqShiftSeparator = separator;

            Label boostTitle = CreateSectionLabel("Amplificación", 20, EqTop + 12);
            panel.Controls.Add(boostTitle);
            eqShiftTitle = boostTitle;

            eqPanel = new EqualizerPanel();
            eqPanel.Location = new Point(20, EqTop);
            eqPanel.Size = new Size(820, 0);
            eqPanel.Visible = false;
            panel.Controls.Add(eqPanel);

            gainValueLabel = new Label();
            gainValueLabel.Text = "100 %  (0.0 dB)";
            gainValueLabel.Font = new Font("Segoe UI Semibold", 10.5F);
            gainValueLabel.ForeColor = AccentColor;
            gainValueLabel.AutoSize = true;
            gainValueLabel.Location = new Point(196, EqTop + 12);
            panel.Controls.Add(gainValueLabel);

            gainSlider = CreateSlider(20, EqTop + 40, 820, 0, 400, 5, 25);
            gainSlider.Value = 100;
            gainSlider.ValueChanged += HandleGainChanged;
            panel.Controls.Add(gainSlider);

            limiterCheck = new CheckBox();
            limiterCheck.Text = "Limitador de picos";
            limiterCheck.AutoSize = true;
            limiterCheck.Location = new Point(20, EqTop + 84);
            limiterCheck.ForeColor = TextColor;
            limiterCheck.BackColor = CardColor;
            limiterCheck.Checked = true;
            limiterCheck.CheckedChanged += HandleLimiterChanged;
            panel.Controls.Add(limiterCheck);

            noiseSeparator = CreateSeparator(20, EqTop + 128, 820);
            panel.Controls.Add(noiseSeparator);

            noiseTitle = CreateSectionLabel("Cancelación de ruido", 20, EqTop + 140);
            panel.Controls.Add(noiseTitle);

            noiseGateValueLabel = new Label();
            noiseGateValueLabel.Text = "Umbral -60 dB";
            noiseGateValueLabel.Font = new Font("Segoe UI Semibold", 9.5F);
            noiseGateValueLabel.ForeColor = AccentColor;
            noiseGateValueLabel.AutoSize = true;
            noiseGateValueLabel.Location = new Point(240, EqTop + 140);
            panel.Controls.Add(noiseGateValueLabel);

            noiseLevelLabel = new Label();
            noiseLevelLabel.Text = "Nivel \u2212\u221E dB";
            noiseLevelLabel.Font = new Font("Segoe UI Semibold", 9.5F);
            noiseLevelLabel.ForeColor = MutedColor;
            noiseLevelLabel.AutoSize = true;
            noiseLevelLabel.Location = new Point(430, EqTop + 140);
            panel.Controls.Add(noiseLevelLabel);

            noiseGateBar = new NoiseGateBar();
            noiseGateBar.Location = new Point(20, EqTop + 168);
            noiseGateBar.Size = new Size(820, 42);
            noiseGateBar.MinimumDb = NoiseGateSampleProvider.MinimumThresholdDb;
            noiseGateBar.MaximumDb = NoiseGateSampleProvider.MaximumThresholdDb;
            noiseGateBar.BackColor = CardColor;
            noiseGateBar.AccentColor = AccentColor;
            noiseGateBar.WarningColor = WarningColor;
            noiseGateBar.TrackColor = Color.FromArgb(62, 62, 77);
            noiseGateBar.ThumbColor = Color.FromArgb(242, 243, 248);
            noiseGateBar.ValueChanged += HandleNoiseGateChanged;
            panel.Controls.Add(noiseGateBar);

            panel.Resize += delegate
            {
                int width = panel.ClientSize.Width - 40;
                if (width < 300)
                {
                    width = 300;
                }
                inputCombo.Width = width;
                destinationCombo.Width = width;
                eqPanel.Width = width;
                gainSlider.Width = width;
                separator.Width = width;
                noiseSeparator.Width = width;
                noiseGateBar.Width = width;
            };

            return panel;
        }

        private void ToggleEqualizer()
        {
            if (eqAnimationTimer == null || eqAnimationTimer.Enabled)
            {
                return;
            }

            if (eqOpen)
            {
                CloseEqualizer();
            }
            else
            {
                OpenEqualizer();
            }
        }

        private void OpenEqualizer()
        {
            eqOpen = true;
            UpdateEqButtonAppearance();
            eqClosedClientHeight = ClientSize.Height;

            int panelHeight = EqualizerPanel.PanelHeight;
            int amplificationBottom = noiseGateBar.Bottom;
            int emptySpace = entradaPanel.ClientSize.Height - amplificationBottom;
            if (emptySpace < 0)
            {
                emptySpace = 0;
            }

            int extra = panelHeight - emptySpace + 12;
            if (extra < 0)
            {
                extra = 0;
            }

            int nonClientHeight = Height - ClientSize.Height;
            int maxClientHeight = Screen.FromControl(this).WorkingArea.Height - nonClientHeight - 12;
            if (eqClosedClientHeight + extra > maxClientHeight)
            {
                extra = Math.Max(0, maxClientHeight - eqClosedClientHeight);
            }

            eqExtraHeight = extra;
            eqPanel.Visible = true;
            eqPanel.Height = 0;
            ShiftAmplification(0);
            StartEqAnimation(0, panelHeight, 0, panelHeight, eqClosedClientHeight, eqClosedClientHeight + extra);
        }

        private void CloseEqualizer()
        {
            eqOpen = false;
            UpdateEqButtonAppearance();

            if (eqPanel.IsMonitoring)
            {
                try
                {
                    engine.StopMonitoring();
                }
                catch (Exception)
                {
                }
                eqPanel.SetMonitoringState(false);
            }

            int startHeight = eqPanel.Height;
            int targetFormHeight = ClientSize.Height - eqExtraHeight;
            if (targetFormHeight < 320)
            {
                targetFormHeight = 320;
            }

            StartEqAnimation(startHeight, 0, startHeight, 0, ClientSize.Height, targetFormHeight);
        }

        private void StartEqAnimation(
            int startHeight,
            int endHeight,
            int startShift,
            int endShift,
            int startFormHeight,
            int endFormHeight)
        {
            eqAnimationStartHeight = startHeight;
            eqAnimationEndHeight = endHeight;
            eqAnimationStartAmpTop = startShift;
            eqAnimationEndAmpTop = endShift;
            eqAnimationStartFormHeight = startFormHeight;
            eqAnimationEndFormHeight = endFormHeight;

            eqAnimationWatch.Reset();
            eqAnimationWatch.Start();
            eqAnimationTimer.Start();
        }

        private void HandleEqAnimationTick(object sender, EventArgs e)
        {
            double progress = (double)eqAnimationWatch.ElapsedMilliseconds / 220.0;
            if (progress > 1.0)
            {
                progress = 1.0;
            }
            double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);

            int height = eqAnimationStartHeight + (int)Math.Round((eqAnimationEndHeight - eqAnimationStartHeight) * eased);
            int shift = eqAnimationStartAmpTop + (int)Math.Round((eqAnimationEndAmpTop - eqAnimationStartAmpTop) * eased);
            int formHeight = eqAnimationStartFormHeight + (int)Math.Round((eqAnimationEndFormHeight - eqAnimationStartFormHeight) * eased);

            eqPanel.Height = height;
            ShiftAmplification(shift);
            if (ClientSize.Height != formHeight)
            {
                ClientSize = new Size(ClientSize.Width, formHeight);
            }

            if (progress < 1.0)
            {
                return;
            }

            eqAnimationTimer.Stop();
            eqAnimationWatch.Reset();

            if (eqOpen)
            {
                eqPanel.Height = EqualizerPanel.PanelHeight;
                ShiftAmplification(EqualizerPanel.PanelHeight);
            }
            else
            {
                eqPanel.Visible = false;
                eqPanel.Height = 0;
                ShiftAmplification(0);
            }

            if (ClientSize.Height != eqAnimationEndFormHeight)
            {
                ClientSize = new Size(ClientSize.Width, eqAnimationEndFormHeight);
            }
        }

        private void ShiftAmplification(int offset)
        {
            if (eqShiftSeparator != null)
            {
                eqShiftSeparator.Top = EqTop + offset;
            }
            if (eqShiftTitle != null)
            {
                eqShiftTitle.Top = EqTop + 12 + offset;
            }
            if (gainValueLabel != null)
            {
                gainValueLabel.Top = EqTop + 12 + offset;
            }
            if (gainSlider != null)
            {
                gainSlider.Top = EqTop + 40 + offset;
            }
            if (limiterCheck != null)
            {
                limiterCheck.Top = EqTop + 84 + offset;
            }
            if (noiseSeparator != null)
            {
                noiseSeparator.Top = EqTop + 128 + offset;
            }
            if (noiseTitle != null)
            {
                noiseTitle.Top = EqTop + 140 + offset;
            }
            if (noiseGateValueLabel != null)
            {
                noiseGateValueLabel.Top = EqTop + 140 + offset;
            }
            if (noiseLevelLabel != null)
            {
                noiseLevelLabel.Top = EqTop + 140 + offset;
            }
            if (noiseGateBar != null)
            {
                noiseGateBar.Top = EqTop + 168 + offset;
            }
        }

        private void UpdateEqButtonAppearance()
        {
            if (eqButton == null)
            {
                return;
            }

            if (eqOpen)
            {
                eqButton.BackColor = AccentColor;
                eqButton.ForeColor = Color.FromArgb(16, 34, 34);
                eqButton.FlatAppearance.BorderColor = AccentColor;
            }
            else
            {
                eqButton.BackColor = InputColor;
                eqButton.ForeColor = TextColor;
                eqButton.FlatAppearance.BorderColor = ButtonBorderColor;
            }
        }

        private void HandleEqGainsChanged(object sender, EventArgs e)
        {
            if (eqPanel != null)
            {
                engine.SetEqualizerGains(eqPanel.Gains);
            }
        }

        private void HandleEqStatusMessage(string message)
        {
            ShowStatus(message, MutedColor);
        }

        private Panel BuildSettingsPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = CardColor;

            Label title = CreateSectionLabel("Ajustes", 20, 16);
            panel.Controls.Add(title);

            autoStartCheck = new CheckBox();
            autoStartCheck.Text = "Iniciar con Windows";
            autoStartCheck.AutoSize = true;
            autoStartCheck.Location = new Point(20, 56);
            autoStartCheck.ForeColor = TextColor;
            autoStartCheck.BackColor = CardColor;
            autoStartCheck.CheckedChanged += HandleAutoStartChanged;
            panel.Controls.Add(autoStartCheck);

            Label startupNote = CreateCaption("Se inicia al iniciar sesión; no requiere permisos de administrador.", 20, 84);
            panel.Controls.Add(startupNote);

            startMinimizedCheck = new CheckBox();
            startMinimizedCheck.Text = "Iniciar en segundo plano (bandeja)";
            startMinimizedCheck.AutoSize = true;
            startMinimizedCheck.Location = new Point(20, 112);
            startMinimizedCheck.ForeColor = TextColor;
            startMinimizedCheck.BackColor = CardColor;
            startMinimizedCheck.CheckedChanged += HandleStartMinimizedChanged;
            panel.Controls.Add(startMinimizedCheck);

            Label trayNote = CreateCaption("Al cerrar la ventana, bitAudio seguirá activo en la bandeja.", 20, 144);
            panel.Controls.Add(trayNote);

            LinkLabel startupLink = new LinkLabel();
            startupLink.Text = "Abrir aplicaciones de inicio de Windows";
            startupLink.AutoSize = true;
            startupLink.Location = new Point(20, 178);
            startupLink.LinkColor = AccentColor;
            startupLink.ActiveLinkColor = TextColor;
            startupLink.VisitedLinkColor = AccentColor;
            startupLink.LinkBehavior = LinkBehavior.HoverUnderline;
            startupLink.BackColor = CardColor;
            startupLink.LinkClicked += HandleStartupSettingsClick;
            panel.Controls.Add(startupLink);

            return panel;
        }

        private void RefreshStartupSettingsUi()
        {
            if (autoStartCheck == null)
            {
                return;
            }

            suppressSettingsEvents = true;
            bool enabled = StartupManager.IsAutoStartEnabled();
            autoStartCheck.Checked = enabled;
            startMinimizedCheck.Checked = StartupManager.GetStartMinimized();
            startMinimizedCheck.Enabled = enabled;
            suppressSettingsEvents = false;
        }

        private void HandleAutoStartChanged(object sender, EventArgs e)
        {
            if (suppressSettingsEvents)
            {
                return;
            }

            try
            {
                if (autoStartCheck.Checked)
                {
                    StartupManager.EnableAutoStart(Application.ExecutablePath);
                }
                else
                {
                    StartupManager.DisableAutoStart();
                }

                startMinimizedCheck.Enabled = autoStartCheck.Checked;
                ShowStatus(
                    autoStartCheck.Checked ? "Inicio automático activado." : "Inicio automático desactivado.",
                    MutedColor);
            }
            catch (Exception exception)
            {
                suppressSettingsEvents = true;
                autoStartCheck.Checked = StartupManager.IsAutoStartEnabled();
                suppressSettingsEvents = false;
                ShowStatus("No se pudo cambiar el inicio automático: " + exception.Message, ErrorColor);
            }
        }

        private void HandleStartMinimizedChanged(object sender, EventArgs e)
        {
            if (suppressSettingsEvents)
            {
                return;
            }

            StartupManager.SetStartMinimized(startMinimizedCheck.Checked);
            ShowStatus(
                startMinimizedCheck.Checked ? "Se iniciará en segundo plano." : "Se mostrará la ventana al iniciar.",
                MutedColor);
        }

        private void HandleStartupSettingsClick(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true });
            }
            catch (Exception)
            {
                try
                {
                    Process.Start("taskmgr.exe");
                }
                catch (Exception exception)
                {
                    ShowStatus("No se pudo abrir el inicio de Windows: " + exception.Message, ErrorColor);
                }
            }
        }

        private static Label CreateSectionLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            label.Font = new Font("Segoe UI Semibold", 11F);
            label.ForeColor = TextColor;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static Label CreateCaption(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            label.ForeColor = MutedColor;
            label.Font = new Font("Segoe UI", 9F);
            label.BackColor = Color.Transparent;
            return label;
        }

        private static Panel CreateSeparator(int x, int y, int width)
        {
            Panel separator = new Panel();
            separator.Location = new Point(x, y);
            separator.Size = new Size(width, 1);
            separator.BackColor = Color.FromArgb(55, 55, 68);
            return separator;
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
            combo.Font = new Font("Segoe UI", 10F);
            combo.IntegralHeight = false;
            combo.MaxDropDownItems = 10;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 22;
            combo.DropDownWidth = Math.Max(width, 520);
            combo.DrawItem += HandleComboDrawItem;
            return combo;
        }

        private static SlimSlider CreateSlider(int x, int y, int width, int minimum, int maximum, int smallChange, int largeChange)
        {
            SlimSlider slider = new SlimSlider();
            slider.Location = new Point(x, y);
            slider.Width = width;
            slider.Height = 30;
            slider.Minimum = minimum;
            slider.Maximum = maximum;
            slider.SmallChange = smallChange;
            slider.LargeChange = largeChange;
            slider.BackColor = CardColor;
            slider.AccentColor = AccentColor;
            slider.TrackColor = Color.FromArgb(62, 62, 77);
            slider.ThumbColor = Color.FromArgb(242, 243, 248);
            return slider;
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
            button.FlatAppearance.MouseOverBackColor = CardHoverColor;
            button.BackColor = InputColor;
            button.ForeColor = TextColor;
            button.Font = new Font("Segoe UI Semibold", 9F);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private static Button CreateTabButton(string text, int x, int y)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(108, 42);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = CardHoverColor;
            button.BackColor = BackgroundColor;
            button.ForeColor = MutedColor;
            button.Font = new Font("Segoe UI Semibold", 10F);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private void SelectTab(int index)
        {
            if (salidaPanel == null)
            {
                return;
            }

            entradaPanel.Visible = index == 0;
            salidaPanel.Visible = index == 1;
            settingsPanel.Visible = index == 2;

            StyleTab(tabEntradaButton, index == 0);
            StyleTab(tabSalidaButton, index == 1);
            StyleTab(tabAjustesButton, index == 2);

            if (index == 2)
            {
                RefreshStartupSettingsUi();
            }
        }

        private static void StyleTab(Button button, bool selected)
        {
            button.BackColor = selected ? CardColor : BackgroundColor;
            button.ForeColor = selected ? AccentColor : MutedColor;
            button.FlatAppearance.MouseOverBackColor = selected ? CardColor : CardHoverColor;
        }

        private void HandleHelpClick(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string text =
                "1. Instala VB-CABLE desde la pestaña Entrada.\r\n"
                + "2. En bitAudio: Micrófono = tu micrófono real; Destino = CABLE Input.\r\n"
                + "3. La app procesa automáticamente al abrirse.\r\n"
                + "4. En Windows, Discord u OBS elige CABLE Output como micrófono.\r\n\r\n"
                + "La pestaña Salida solo controla el volumen de Windows de tus auriculares.";
            MessageBox.Show(this, text, "Ayuda", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void HandleShown(object sender, EventArgs e)
        {
            bool promptAlreadyShown = StartupManager.WasSetupPromptShown();

            if (launchedFromStartup)
            {
                StartupManager.MarkSetupPromptShown();
            }
            else if (!promptAlreadyShown)
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "¿Quieres que bitAudio se inicie automáticamente con Windows?\r\n\r\nPodrás cambiarlo en Ajustes.",
                    "Inicio automático",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (answer == DialogResult.Yes)
                {
                    try
                    {
                        StartupManager.EnableAutoStart(Application.ExecutablePath);
                    }
                    catch (Exception exception)
                    {
                        ShowStatus("No se pudo activar el inicio automático: " + exception.Message, ErrorColor);
                    }
                }

                StartupManager.MarkSetupPromptShown();
            }
            else
            {
                try
                {
                    StartupManager.EnsureAutoStartPath(Application.ExecutablePath);
                }
                catch (Exception)
                {
                }
            }

            RefreshStartupSettingsUi();

            try
            {
                RefreshDevices();
            }
            catch (Exception exception)
            {
                ShowStatus("Error al detectar dispositivos: " + exception.Message, ErrorColor);
            }

            syncTimer.Start();
            meterTimer.Start();

            if (startInBackground)
            {
                HideToTray(false);
            }
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray(true);
                return;
            }

                closing = true;
                syncTimer.Stop();
                if (meterTimer != null)
                {
                    meterTimer.Stop();
                }
                if (eqAnimationTimer != null)
                {
                    eqAnimationTimer.Stop();
                }
                if (eqPanel != null)
                {
                    eqPanel.SaveState();
                }
                try
                {
                    engine.Stop();
                }
                catch (Exception)
                {
                }
                engine.Dispose();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            if (trayMenu != null)
            {
                trayMenu.Dispose();
                trayMenu = null;
            }
        }

        private void CreateTrayIcon()
        {
            try
            {
                Icon appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (appIcon != null)
                {
                    Icon = appIcon;
                }
            }
            catch (Exception)
            {
            }

            trayIcon = new NotifyIcon();
            trayIcon.Icon = Icon != null ? Icon : SystemIcons.Application;
            trayIcon.Text = "bitAudio";
            trayIcon.Visible = true;

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Abrir bitAudio", null, HandleTrayOpen);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Salir", null, HandleTrayExit);
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.MouseClick += HandleTrayMouseClick;
        }

        private void HandleTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                HandleTrayOpen(sender, EventArgs.Empty);
            }
        }

        private void HandleResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && Visible && !closing)
            {
                HideToTray(!startInBackground);
            }
        }

        private void HideToTray(bool showBalloon)
        {
            if (closing || hidingToTray)
            {
                return;
            }

            if (eqPanel != null && eqPanel.IsMonitoring)
            {
                try
                {
                    engine.StopMonitoring();
                }
                catch (Exception)
                {
                }
                eqPanel.SetMonitoringState(false);
            }

            hidingToTray = true;
            try
            {
                Hide();

                if (showBalloon && !trayBalloonShown && trayIcon != null)
                {
                    trayBalloonShown = true;
                    trayIcon.ShowBalloonTip(
                        3000,
                        "bitAudio",
                        "Sigue activo en segundo plano. Haz clic en el icono para abrirlo.",
                        ToolTipIcon.Info);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                hidingToTray = false;
            }
        }

        private void HandleTrayOpen(object sender, EventArgs e)
        {
            if (closing)
            {
                return;
            }

            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void HandleTrayExit(object sender, EventArgs e)
        {
            allowExit = true;
            Close();
        }

        private void RefreshDevices()
        {
            if (engine.IsRunning)
            {
                StopProcessing();
            }

            string previousInputId = GetSelectedId(inputCombo);
            string previousDestinationId = GetSelectedId(destinationCombo);
            string previousOutputId = GetSelectedId(outputCombo);

            List<AudioDeviceInfo> newInputs = engine.GetInputDevices();
            List<AudioDeviceInfo> newOutputs = engine.GetOutputDevices();

            List<AudioDeviceInfo> volumeOutputs = new List<AudioDeviceInfo>();
            List<AudioDeviceInfo> cableDestinations = new List<AudioDeviceInfo>();
            foreach (AudioDeviceInfo device in newOutputs)
            {
                if (AudioDeviceInfo.IsCableRenderName(device.Name))
                {
                    cableDestinations.Add(device);
                }
                else if (!device.IsVirtual)
                {
                    volumeOutputs.Add(device);
                }
            }

            suppressUiEvents = true;
            try
            {
                inputDevices = newInputs;
                outputDevices = volumeOutputs;
                destinationDevices = cableDestinations;

                inputCombo.Items.Clear();
                outputCombo.Items.Clear();
                destinationCombo.Items.Clear();
                inputCombo.Items.Add("— Selecciona tu micrófono —");
                outputCombo.Items.Add("— Selecciona una salida —");
                destinationCombo.Items.Add("— Instala VB-CABLE —");

                foreach (AudioDeviceInfo device in inputDevices)
                {
                    inputCombo.Items.Add(device);
                }
                foreach (AudioDeviceInfo device in outputDevices)
                {
                    outputCombo.Items.Add(device);
                }
                foreach (AudioDeviceInfo device in destinationDevices)
                {
                    destinationCombo.Items.Add(device);
                }

                SelectDevice(inputCombo, inputDevices, previousInputId, PickInputDevice(inputDevices));
                SelectDevice(destinationCombo, destinationDevices, previousDestinationId, PickDestinationDevice(destinationDevices));
                SelectDevice(outputCombo, outputDevices, previousOutputId, PickOutputDevice(outputDevices));
            }
            finally
            {
                suppressUiEvents = false;
            }

            AudioDeviceInfo selectedOutput = outputCombo.SelectedItem as AudioDeviceInfo;

            try
            {
                engine.SelectVolumeOutput(selectedOutput != null ? selectedOutput.Id : null);
            }
            catch (Exception)
            {
            }

            SyncOutputVolume();
            UpdateHint();
            StartProcessing();
        }

        private static string GetSelectedId(ComboBox combo)
        {
            AudioDeviceInfo info = combo.SelectedItem as AudioDeviceInfo;
            return info != null ? info.Id : null;
        }

        private static void SelectDevice(ComboBox combo, List<AudioDeviceInfo> devices, string previousId, AudioDeviceInfo fallback)
        {
            int index = -1;
            if (!string.IsNullOrEmpty(previousId))
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i].Id == previousId)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0 && fallback != null)
            {
                index = devices.IndexOf(fallback);
            }

            combo.SelectedIndex = index < 0 ? 0 : index + 1;
        }

        private static AudioDeviceInfo PickInputDevice(List<AudioDeviceInfo> devices)
        {
            foreach (AudioDeviceInfo device in devices)
            {
                if (device.IsDefault && !AudioDeviceInfo.IsCableCaptureName(device.Name))
                {
                    return device;
                }
            }

            foreach (AudioDeviceInfo device in devices)
            {
                if (!AudioDeviceInfo.IsCableCaptureName(device.Name))
                {
                    return device;
                }
            }

            return null;
        }

        private static AudioDeviceInfo PickDestinationDevice(List<AudioDeviceInfo> devices)
        {
            foreach (AudioDeviceInfo device in devices)
            {
                if (AudioDeviceInfo.IsCableRenderName(device.Name))
                {
                    return device;
                }
            }

            return null;
        }

        private static AudioDeviceInfo PickOutputDevice(List<AudioDeviceInfo> devices)
        {
            foreach (AudioDeviceInfo device in devices)
            {
                if (device.IsDefault && !device.IsVirtual)
                {
                    return device;
                }
            }

            foreach (AudioDeviceInfo device in devices)
            {
                if (device.IsVirtual)
                {
                    continue;
                }

                string name = device.Name.ToLowerInvariant();
                if (name.Contains("headset")
                    || name.Contains("auricular")
                    || name.Contains("headphone")
                    || name.Contains("altavoces")
                    || name.Contains("speakers"))
                {
                    return device;
                }
            }

            foreach (AudioDeviceInfo device in devices)
            {
                if (!device.IsVirtual)
                {
                    return device;
                }
            }

            return null;
        }

        private void HandleRefreshClick(object sender, EventArgs e)
        {
            try
            {
                RefreshDevices();
                ShowStatus("Dispositivos actualizados.", MutedColor);
            }
            catch (Exception exception)
            {
                ShowStatus("No se pudieron actualizar los dispositivos: " + exception.Message, ErrorColor);
            }
        }

        private void HandleSoundClick(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
            }
            catch (Exception)
            {
                try
                {
                    Process.Start("control.exe", "mmsys.cpl");
                }
                catch (Exception exception)
                {
                    ShowStatus("No se pudo abrir la configuración de sonido: " + exception.Message, ErrorColor);
                }
            }
        }

        private void HandleCableLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                ShowStatus("No se pudo abrir el navegador: " + exception.Message, ErrorColor);
            }
        }

        private void HandleInputChanged(object sender, EventArgs e)
        {
            if (suppressUiEvents)
            {
                return;
            }

            RestartProcessing();
        }

        private void HandleDestinationChanged(object sender, EventArgs e)
        {
            if (suppressUiEvents)
            {
                return;
            }

            UpdateHint();
            RestartProcessing();
        }

        private void HandleOutputChanged(object sender, EventArgs e)
        {
            if (suppressUiEvents)
            {
                return;
            }

            AudioDeviceInfo info = outputCombo.SelectedItem as AudioDeviceInfo;
            try
            {
                engine.SelectVolumeOutput(info != null ? info.Id : null);
            }
            catch (Exception)
            {
            }

            SyncOutputVolume();

            if (engine.IsRunning && eqPanel != null && eqPanel.IsMonitoring)
            {
                if (info == null)
                {
                    try
                    {
                        engine.StopMonitoring();
                    }
                    catch (Exception)
                    {
                    }
                    eqPanel.SetMonitoringState(false);
                }
                else
                {
                    try
                    {
                        engine.StartMonitoring(info.Id);
                    }
                    catch (Exception exception)
                    {
                        eqPanel.SetMonitoringState(false);
                        ShowStatus("Monitor de micrófono: " + exception.Message, WarningColor);
                    }
                }
            }
        }

        private void HandleLimiterChanged(object sender, EventArgs e)
        {
            if (!suppressUiEvents)
            {
                engine.LimiterEnabled = limiterCheck.Checked;
            }
        }

        private void StartProcessing()
        {
            AudioDeviceInfo input = inputCombo.SelectedItem as AudioDeviceInfo;
            AudioDeviceInfo destination = destinationCombo.SelectedItem as AudioDeviceInfo;

            if (input == null)
            {
                if (eqPanel.IsMonitoring)
                {
                    eqPanel.SetMonitoringState(false);
                }
                SelectTab(0);
                ShowStatus("Selecciona tu micrófono.", WarningColor);
                return;
            }

            if (destination == null)
            {
                if (eqPanel.IsMonitoring)
                {
                    eqPanel.SetMonitoringState(false);
                }
                SelectTab(0);
                ShowStatus("No activo.", MutedColor);
                return;
            }

            if (AudioDeviceInfo.IsCableCaptureName(input.Name) && AudioDeviceInfo.IsCableRenderName(destination.Name))
            {
                if (eqPanel.IsMonitoring)
                {
                    eqPanel.SetMonitoringState(false);
                }
                ShowStatus("Selecciona un micrófono real, no CABLE Output.", ErrorColor);
                return;
            }

            const int latency = 10;

            try
            {
                engine.Start(
                    input.Id,
                    destination.Id,
                    gainSlider.Value / 100f,
                    limiterCheck.Checked,
                    latency,
                    latency);

                UpdateActiveStatus();

                if (eqPanel.IsMonitoring)
                {
                    AudioDeviceInfo monitorOutput = outputCombo.SelectedItem as AudioDeviceInfo;
                    if (monitorOutput == null)
                    {
                        eqPanel.SetMonitoringState(false);
                    }
                    else
                    {
                        try
                        {
                            engine.StartMonitoring(monitorOutput.Id);
                        }
                        catch (Exception monitorException)
                        {
                            eqPanel.SetMonitoringState(false);
                            ShowStatus("Monitor de micrófono: " + monitorException.Message, WarningColor);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                eqPanel.SetMonitoringState(false);
                ShowStatus("No se pudo iniciar: " + exception.Message, ErrorColor);
            }
        }

        private void HandleMonitoringToggleRequested(object sender, EventArgs e)
        {
            if (!eqPanel.IsMonitoring)
            {
                try
                {
                    engine.StopMonitoring();
                }
                catch (Exception)
                {
                }
                ShowStatus("Monitor de micrófono apagado.", MutedColor);
                return;
            }

            if (!engine.IsRunning)
            {
                eqPanel.SetMonitoringState(false);
                ShowStatus("El monitor necesita el procesamiento activo.", WarningColor);
                return;
            }

            AudioDeviceInfo output = outputCombo.SelectedItem as AudioDeviceInfo;
            if (output == null)
            {
                eqPanel.SetMonitoringState(false);
                ShowStatus("Selecciona una salida en la pestaña Salida.", WarningColor);
                return;
            }

            try
            {
                engine.StartMonitoring(output.Id);
                ShowStatus("Escuchando el micrófono procesado. Usa auriculares.", WarningColor);
            }
            catch (Exception exception)
            {
                eqPanel.SetMonitoringState(false);
                ShowStatus("No se pudo escuchar el micrófono: " + exception.Message, ErrorColor);
            }
        }

        private void UpdateActiveStatus()
        {
            if (engine.IsRunning)
            {
                ShowStatus("Activo · " + FormatGain(gainSlider.Value), SuccessColor);
            }
        }

        private void StopProcessing()
        {
            try
            {
                engine.Stop();
            }
            catch (Exception)
            {
            }
        }

        private void RestartProcessing()
        {
            if (restarting)
            {
                return;
            }

            restarting = true;
            try
            {
                if (engine.IsRunning)
                {
                    StopProcessing();
                }
                StartProcessing();
            }
            finally
            {
                restarting = false;
            }
        }

        private void HandleOutputVolumeChanged(object sender, EventArgs e)
        {
            if (suppressUiEvents)
            {
                return;
            }

            engine.TrySetOutputVolume(outputVolumeSlider.Value / 100f);
            outputVolumeValue.Text = outputVolumeSlider.Value + " %";
        }

        private void HandleMuteChanged(object sender, EventArgs e)
        {
            if (suppressUiEvents)
            {
                return;
            }

            engine.TrySetOutputMute(muteCheck.Checked);
            UpdateMuteAppearance();
        }

        private void UpdateMuteAppearance()
        {
            if (muteCheck.Checked)
            {
                muteCheck.Text = "Silenciado";
                muteCheck.BackColor = Color.FromArgb(92, 45, 52);
                muteCheck.ForeColor = ErrorColor;
            }
            else
            {
                muteCheck.Text = "Silenciar";
                muteCheck.BackColor = InputColor;
                muteCheck.ForeColor = TextColor;
            }
        }

        private void HandleGainChanged(object sender, EventArgs e)
        {
            if (suppressUiEvents)
            {
                return;
            }

            engine.Gain = gainSlider.Value / 100f;
            try
            {
                StartupManager.SetGainPercent(gainSlider.Value);
            }
            catch (Exception)
            {
            }
            UpdateGainDisplay();
            UpdateActiveStatus();
        }

        private void UpdateGainDisplay()
        {
            int percent = gainSlider.Value;
            string db;
            if (percent <= 0)
            {
                db = "\u2212\u221E dB";
            }
            else
            {
                double value = 20.0 * Math.Log10(percent / 100.0);
                db = value.ToString("+0.0;-0.0;0.0") + " dB";
            }

            gainValueLabel.Text = percent + " %  (" + db + ")";
            gainValueLabel.ForeColor = percent > 100 ? WarningColor : AccentColor;
        }

        private void HandleMeterTimer(object sender, EventArgs e)
        {
            if (closing || noiseGateBar == null)
            {
                return;
            }

            float level = engine.InputLevelDb;
            noiseGateBar.LevelDb = level;
            noiseLevelLabel.Text = "Nivel " + FormatNoiseLevel(level);

            if (level <= -89.5f)
            {
                noiseLevelLabel.ForeColor = MutedColor;
            }
            else
            {
                noiseLevelLabel.ForeColor = level >= noiseGateBar.ValueDb ? SuccessColor : WarningColor;
            }
        }

        private void HandleNoiseGateChanged(object sender, EventArgs e)
        {
            if (suppressNoiseGateEvents)
            {
                return;
            }

            int db = noiseGateBar.ValueDb;
            engine.SetNoiseGateThreshold(db);
            try
            {
                StartupManager.SetNoiseGateThresholdDb(db);
            }
            catch (Exception)
            {
            }
            noiseGateValueLabel.Text = "Umbral " + db + " dB";
        }

        private static string FormatNoiseLevel(float db)
        {
            if (db <= -89.5f)
            {
                return "\u2212\u221E dB";
            }
            return Math.Round(db).ToString("0") + " dB";
        }

        private static string FormatGain(int percent)
        {
            if (percent <= 0)
            {
                return "silencio";
            }
            double value = 20.0 * Math.Log10(percent / 100.0);
            return percent + " % (" + value.ToString("+0.0;-0.0;0.0") + " dB)";
        }

        private void HandleSyncTimer(object sender, EventArgs e)
        {
            if (closing || IsDisposed)
            {
                return;
            }

            SyncOutputVolume();

            hintTick++;
            if (hintTick % 8 == 0)
            {
                UpdateHint();
            }
        }

        private void SyncOutputVolume()
        {
            float scalar;
            if (!engine.TryGetOutputVolume(out scalar))
            {
                if (outputVolumeSlider.Enabled)
                {
                    suppressUiEvents = true;
                    outputVolumeSlider.Enabled = false;
                    muteCheck.Enabled = false;
                    suppressUiEvents = false;
                }
                outputVolumeValue.Text = "—";
                return;
            }

            if (!outputVolumeSlider.Enabled)
            {
                suppressUiEvents = true;
                outputVolumeSlider.Enabled = true;
                muteCheck.Enabled = true;
                suppressUiEvents = false;
            }

            if (!outputVolumeSlider.IsDragging)
            {
                int percent = (int)Math.Round(scalar * 100f);
                if (outputVolumeSlider.Value != percent)
                {
                    suppressUiEvents = true;
                    outputVolumeSlider.Value = percent;
                    suppressUiEvents = false;
                }
            }

            outputVolumeValue.Text = outputVolumeSlider.Value + " %";

            bool muted;
            if (engine.TryGetOutputMute(out muted) && muteCheck.Checked != muted)
            {
                suppressUiEvents = true;
                muteCheck.Checked = muted;
                suppressUiEvents = false;
                UpdateMuteAppearance();
            }
        }

        private void UpdateHint()
        {
            if (cableLink == null || IsDisposed)
            {
                return;
            }

            cableLink.Visible = destinationDevices.Count == 0;
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

        private void HandleEngineStatus(string message)
        {
            if (closing || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(HandleEngineStatus), message);
                return;
            }

            statusLabel.ForeColor = MutedColor;
            statusLabel.Text = message;
        }

        private void HandleEngineError(string message)
        {
            if (closing || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(HandleEngineError), message);
                return;
            }

            try
            {
                engine.Stop();
            }
            catch (Exception)
            {
            }

            if (eqPanel != null && eqPanel.IsMonitoring)
            {
                eqPanel.SetMonitoringState(false);
            }

            ShowStatus(message, ErrorColor);
        }

        private void ShowStatus(string text, Color color)
        {
            statusLabel.ForeColor = color;
            statusLabel.Text = text;

            if (color == ErrorColor && trayIcon != null && trayIcon.Visible && !Visible)
            {
                trayIcon.ShowBalloonTip(3000, "bitAudio", text, ToolTipIcon.Warning);
            }
        }
    }
}
