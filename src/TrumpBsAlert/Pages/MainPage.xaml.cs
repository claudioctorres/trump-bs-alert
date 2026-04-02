using SkiaSharp;
using SkiaSharp.Views.Maui;
using TrumpBsAlert.ViewModels;

namespace TrumpBsAlert.Pages;

public partial class MainPage : ContentPage
{
    private static readonly SKFont LabelFont = new(SKTypeface.Default, 11);

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainViewModel vm)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(vm.ChartData) or nameof(vm.ChartThreshold))
                    ChartCanvas.InvalidateSurface();
            };
            await vm.InitializeAsync();
        }
    }

    private void OnChartPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (BindingContext is not MainViewModel vm)
            return;

        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Transparent);

        var data = vm.ChartData;
        if (data is null || data.Count < 2)
            return;

        const float paddingLeft = 60;
        const float paddingRight = 16;
        const float paddingTop = 16;
        const float paddingBottom = 30;

        float chartW = info.Width - paddingLeft - paddingRight;
        float chartH = info.Height - paddingTop - paddingBottom;

        var minRate = (float)data.Min(d => d.Close);
        var maxRate = (float)data.Max(d => d.Close);
        var range = maxRate - minRate;
        if (range < 0.0001f) range = 0.01f;
        var pad = range * 0.1f;
        minRate -= pad;
        maxRate += pad;
        range = maxRate - minRate;

        float MapX(int i) => paddingLeft + (i * chartW / (data.Count - 1));
        float MapY(float v) => paddingTop + chartH - ((v - minRate) / range * chartH);

        // Threshold shading
        if (vm.ChartThreshold is { } threshold)
        {
            var thresholdY = MapY((float)threshold);
            if (thresholdY > paddingTop)
            {
                using var shadePaint = new SKPaint
                {
                    Color = SKColors.Red.WithAlpha(30),
                    Style = SKPaintStyle.Fill,
                };
                canvas.DrawRect(paddingLeft, paddingTop, chartW, thresholdY - paddingTop, shadePaint);
            }

            using var linePaint = new SKPaint
            {
                Color = SKColors.Red,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
            };
            canvas.DrawLine(paddingLeft, thresholdY, paddingLeft + chartW, thresholdY, linePaint);
        }

        // Line
        using var pathPaint = new SKPaint
        {
            Color = new SKColor(0x1E, 0x90, 0xFF), // DodgerBlue
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };

        using var path = new SKPath();
        path.MoveTo(MapX(0), MapY((float)data[0].Close));
        for (int i = 1; i < data.Count; i++)
            path.LineTo(MapX(i), MapY((float)data[i].Close));
        canvas.DrawPath(path, pathPaint);

        // Dots
        using var dotPaint = new SKPaint
        {
            Color = new SKColor(0x1E, 0x90, 0xFF),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        for (int i = 0; i < data.Count; i++)
            canvas.DrawCircle(MapX(i), MapY((float)data[i].Close), 3, dotPaint);

        // Y-axis labels
        using var labelPaint = new SKPaint
        {
            Color = new SKColor(0xCD, 0xD6, 0xF4),
            IsAntialias = true,
        };
        int ySteps = 5;
        for (int i = 0; i <= ySteps; i++)
        {
            var val = minRate + (range * i / ySteps);
            var y = MapY(val);
            canvas.DrawText(val.ToString("F4"), 4, y + 4, SKTextAlign.Left, LabelFont, labelPaint);
        }

        // X-axis labels (every ~7 days)
        int step = Math.Max(1, data.Count / 4);
        for (int i = 0; i < data.Count; i += step)
        {
            var x = MapX(i);
            canvas.DrawText(data[i].Date.ToString("dd/MM"), x - 12, info.Height - 4, SKTextAlign.Left, LabelFont, labelPaint);
        }
    }
}
