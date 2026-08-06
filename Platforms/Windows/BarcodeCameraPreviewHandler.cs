using InventorySystem.Controls;
using Microsoft.Maui.Handlers;
using WinUIImage = Microsoft.UI.Xaml.Controls.Image;
using WinUIStretch = Microsoft.UI.Xaml.Media.Stretch;

namespace InventorySystem.Platforms.Windows;

public sealed class BarcodeCameraPreviewHandler : ViewHandler<BarcodeCameraPreview, WinUIImage>
{
    public static readonly IPropertyMapper<BarcodeCameraPreview, BarcodeCameraPreviewHandler> Mapper =
        new PropertyMapper<BarcodeCameraPreview, BarcodeCameraPreviewHandler>(ViewMapper);

    public BarcodeCameraPreviewHandler()
        : base(Mapper)
    {
    }

    protected override WinUIImage CreatePlatformView() => new()
    {
        Stretch = WinUIStretch.Uniform
    };
}
