using Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Boot;
using Services.Cache;

/// <summary>
/// Dedicated SPARK firmware-update tab. One row per area
/// (<see cref="SparkAreas.All"/>): label + file picker + include checkbox.
/// The "Update" button runs <see cref="SparkBatchUpdateService"/> on the
/// selected subset, in canonical order, stop-on-first-failure.
/// </summary>
public sealed class Spark_FirmwareUpdate_WF_Tab : TabPage
{
    private readonly ConnectionManager _connMgr;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<SparkFirmwareArea, AreaRow> _rows = new();
    private Button _btnUpdate = null!;
    private Label _lblStatus = null!;
    private ProgressBar _pbCurrentArea = null!;
    private ProgressBar _pbTotal = null!;

    public Spark_FirmwareUpdate_WF_Tab(
        ConnectionManager connMgr, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(connMgr);
        _connMgr = connMgr;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;

        Name = "tabPageSparkFirmware";
        Text = "SPARK Firmware Update";

        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // area grid
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // action bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // status / progress

        root.Controls.Add(BuildAreaGrid(), 0, 0);
        root.Controls.Add(BuildActionBar(), 0, 1);
        root.Controls.Add(BuildStatusPanel(), 0, 2);

        Controls.Add(root);
    }

    private TableLayoutPanel BuildAreaGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = SparkAreas.All.Count,
            Margin = new Padding(0, 0, 0, 10),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // label
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // path
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // select btn
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));  // include chk

        int row = 0;
        foreach (var def in SparkAreas.All)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var areaRow = BuildAreaRow(def);
            _rows[def.Area] = areaRow;

            grid.Controls.Add(areaRow.Label,    0, row);
            grid.Controls.Add(areaRow.PathBox,  1, row);
            grid.Controls.Add(areaRow.SelectBtn, 2, row);
            grid.Controls.Add(areaRow.IncludeBox, 3, row);
            row++;
        }
        return grid;
    }

    private static AreaRow BuildAreaRow(SparkAreaDefinition def)
    {
        var label = new Label
        {
            Text = def.DisplayName,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3),
        };
        var pathBox = new TextBox
        {
            ReadOnly = true,
            Dock = DockStyle.Fill,
            PlaceholderText = "No file selected",
            Margin = new Padding(3),
        };
        var selectBtn = new Button
        {
            Text = "Select .bin",
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
        };
        var includeBox = new CheckBox
        {
            Text = "Include",
            Dock = DockStyle.Fill,
            Enabled = false,
            CheckAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3),
        };

        var row = new AreaRow(def, label, pathBox, selectBtn, includeBox);

        selectBtn.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Binary Files|*.bin|All Files|*.*",
                Title = $"Select firmware for {def.DisplayName}",
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            pathBox.Text = ofd.FileName;
            includeBox.Enabled = true;
            includeBox.Checked = true;
        };

        return row;
    }

    private FlowLayoutPanel BuildActionBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 5),
        };
        _btnUpdate = new Button
        {
            Text = "Update",
            Width = 140,
            Height = 36,
            Margin = new Padding(0, 0, 10, 0),
        };
        _btnUpdate.Click += BtnUpdate_Click;
        bar.Controls.Add(_btnUpdate);

        _lblStatus = new Label
        {
            Text = "Idle.",
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        bar.Controls.Add(_lblStatus);
        return bar;
    }

    private TableLayoutPanel BuildStatusPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            Text = "Current area:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _pbCurrentArea = new ProgressBar { Dock = DockStyle.Top, Height = 22 };
        panel.Controls.Add(_pbCurrentArea, 1, 0);

        panel.Controls.Add(new Label
        {
            Text = "Total batch:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 1);
        _pbTotal = new ProgressBar { Dock = DockStyle.Top, Height = 22 };
        panel.Controls.Add(_pbTotal, 1, 1);
        return panel;
    }

    private async void BtnUpdate_Click(object? sender, EventArgs e)
    {
        var batch = BuildBatch();
        if (batch.Count == 0)
        {
            MessageBox.Show(
                "Select at least one area (pick a .bin file and tick 'Include').",
                "Nothing to update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var boot = _connMgr.CurrentBoot;
        if (boot is null)
        {
            MessageBox.Show(
                "Select a communication channel first.",
                "No channel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetUiBusy(true);
        _pbCurrentArea.Value = 0;
        _pbTotal.Value = 0;
        SetStatus("Starting batch...");

        int totalAreas = batch.Count;
        int areasDone = 0;
        var orchestrator = new SparkBatchUpdateService(
            boot, _loggerFactory.CreateLogger<SparkBatchUpdateService>());
        orchestrator.AreaStarted += (_, def) => RunOnUi(() =>
        {
            SetStatus($"[{areasDone + 1}/{totalAreas}] {def.DisplayName} — starting...");
            _pbCurrentArea.Value = 0;
        });
        orchestrator.AreaProgress += (_, p) => RunOnUi(() =>
        {
            int pct = p.TotalLength <= 0 ? 0 : (int)(p.Fraction * 100);
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            _pbCurrentArea.Value = pct;
            int batchPct = (int)(((areasDone + p.Fraction) / totalAreas) * 100);
            _pbTotal.Value = Math.Min(100, Math.Max(0, batchPct));
        });
        orchestrator.AreaCompleted += (_, def) => RunOnUi(() =>
        {
            areasDone++;
            _pbCurrentArea.Value = 100;
            _pbTotal.Value = (int)(((double)areasDone / totalAreas) * 100);
            SetStatus($"[{areasDone}/{totalAreas}] {def.DisplayName} — done.");
        });

        try
        {
            await orchestrator.ExecuteAsync(batch);
            SetStatus($"Batch completed: {totalAreas} area(s) updated.");
            MessageBox.Show(
                $"Firmware update completed ({totalAreas} area(s)).",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (SparkBatchUpdateException ex)
        {
            SetStatus($"Failed at {ex.Area.DisplayName} ({ex.Phase}).");
            MessageBox.Show(
                $"Area '{ex.Area.DisplayName}' failed at phase '{ex.Phase}':\n\n{ex.Cause}",
                "Firmware update error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus("Unexpected error.");
            MessageBox.Show(
                $"Unexpected error during firmware update:\n\n{ex.Message}",
                "Firmware update error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }

    private List<SparkBatchItem> BuildBatch()
    {
        var batch = new List<SparkBatchItem>();
        foreach (var row in _rows.Values)
        {
            if (!row.IncludeBox.Checked) continue;
            string path = row.PathBox.Text;
            if (string.IsNullOrWhiteSpace(path)) continue;
            byte[] bytes = File.ReadAllBytes(path);
            batch.Add(new SparkBatchItem(row.Definition.Area, bytes));
        }
        return batch;
    }

    private void SetUiBusy(bool busy)
    {
        _btnUpdate.Enabled = !busy;
        foreach (var row in _rows.Values)
        {
            row.SelectBtn.Enabled = !busy;
            row.IncludeBox.Enabled = !busy && !string.IsNullOrWhiteSpace(row.PathBox.Text);
        }
    }

    private void SetStatus(string text) => RunOnUi(() => _lblStatus.Text = text);

    private void RunOnUi(Action a)
    {
        if (InvokeRequired) Invoke(a); else a();
    }

    private sealed record AreaRow(
        SparkAreaDefinition Definition,
        Label Label,
        TextBox PathBox,
        Button SelectBtn,
        CheckBox IncludeBox);
}
