using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace CCCD.WinForms;

public class MainForm : Form
{
    private readonly PictureBox _frontPreview;
    private readonly PictureBox _backPreview;
    private readonly PictureBox _outputPreview;
    private readonly NumericUpDown _copiesInput;
    private readonly Button _cropButton;
    private readonly Button _combineButton;
    private readonly Button _printButton;

    private Bitmap? _frontRaw;
    private Bitmap? _backRaw;
    private Bitmap? _frontCropped;
    private Bitmap? _backCropped;
    private Bitmap? _combined;

    public MainForm()
    {
        Text = "CCCD Cutter & Printer";
        Width = 1180;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        var frontButton = new Button { Text = "Load Front", Width = 140 };
        var backButton = new Button { Text = "Load Back", Width = 140 };
        _cropButton = new Button { Text = "Auto Crop", Width = 140, Enabled = false };
        _combineButton = new Button { Text = "Combine", Width = 140, Enabled = false };
        _printButton = new Button { Text = "Print", Width = 140, Enabled = false };

        _copiesInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 200,
            Value = 1,
            Width = 60
        };

        var copiesLabel = new Label { Text = "Copies:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

        _frontPreview = new PictureBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 520,
            Height = 320
        };

        _backPreview = new PictureBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 520,
            Height = 320
        };

        _outputPreview = new PictureBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 1040,
            Height = 300
        };

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(10),
            FlowDirection = FlowDirection.LeftToRight
        };

        topPanel.Controls.Add(frontButton);
        topPanel.Controls.Add(backButton);
        topPanel.Controls.Add(_cropButton);
        topPanel.Controls.Add(_combineButton);
        topPanel.Controls.Add(copiesLabel);
        topPanel.Controls.Add(_copiesInput);
        topPanel.Controls.Add(_printButton);

        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 340,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        previewPanel.Controls.Add(_frontPreview, 0, 0);
        previewPanel.Controls.Add(_backPreview, 1, 0);

        var outputPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        outputPanel.Controls.Add(_outputPreview);

        Controls.Add(outputPanel);
        Controls.Add(previewPanel);
        Controls.Add(topPanel);

        frontButton.Click += (_, _) => LoadImage(isFront: true);
        backButton.Click += (_, _) => LoadImage(isFront: false);
        _cropButton.Click += (_, _) => CropImages();
        _combineButton.Click += (_, _) => CombineImages();
        _printButton.Click += (_, _) => PrintImages();
    }

    private void LoadImage(bool isFront)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp",
            Title = isFront ? "Select front image" : "Select back image"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var bitmap = new Bitmap(dialog.FileName);
        if (isFront)
        {
            _frontRaw?.Dispose();
            _frontRaw = bitmap;
            _frontPreview.Image = _frontRaw;
        }
        else
        {
            _backRaw?.Dispose();
            _backRaw = bitmap;
            _backPreview.Image = _backRaw;
        }

        _cropButton.Enabled = _frontRaw != null || _backRaw != null;
    }

    private void CropImages()
    {
        if (_frontRaw != null)
        {
            _frontCropped?.Dispose();
            _frontCropped = ImageProcessing.AutoCropCard(_frontRaw);
            _frontPreview.Image = _frontCropped;
        }

        if (_backRaw != null)
        {
            _backCropped?.Dispose();
            _backCropped = ImageProcessing.AutoCropCard(_backRaw);
            _backPreview.Image = _backCropped;
        }

        _combineButton.Enabled = _frontCropped != null && _backCropped != null;
    }

    private void CombineImages()
    {
        if (_frontCropped == null || _backCropped == null)
        {
            MessageBox.Show("Please crop both front and back images first.");
            return;
        }

        _combined?.Dispose();
        _combined = ImageProcessing.CombineSideBySide(_frontCropped, _backCropped);
        _outputPreview.Image = _combined;
        _printButton.Enabled = true;
    }

    private void PrintImages()
    {
        if (_combined == null)
        {
            MessageBox.Show("No combined image to print.");
            return;
        }

        using var printDialog = new PrintDialog();
        using var printDocument = new PrintDocument();

        printDocument.PrintPage += (_, args) =>
        {
            var pageBounds = args.MarginBounds;
            var scaled = ImageProcessing.ScaleToFit(_combined, pageBounds.Size);
            var drawX = pageBounds.Left + (pageBounds.Width - scaled.Width) / 2;
            var drawY = pageBounds.Top + (pageBounds.Height - scaled.Height) / 2;
            args.Graphics.DrawImage(_combined, drawX, drawY, scaled.Width, scaled.Height);
        };

        printDocument.PrinterSettings.Copies = (short)_copiesInput.Value;
        printDialog.Document = printDocument;

        if (printDialog.ShowDialog() == DialogResult.OK)
        {
            printDocument.Print();
        }
    }
}
