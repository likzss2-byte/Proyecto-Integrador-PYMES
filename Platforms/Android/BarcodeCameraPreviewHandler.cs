using Android.Views;
using InventorySystem.Controls;
using Microsoft.Maui.Handlers;

namespace InventorySystem.Platforms.Android;

public sealed class BarcodeCameraPreviewHandler : ViewHandler<BarcodeCameraPreview, TextureView>
{
    public static readonly IPropertyMapper<BarcodeCameraPreview, BarcodeCameraPreviewHandler> Mapper =
        new PropertyMapper<BarcodeCameraPreview, BarcodeCameraPreviewHandler>(ViewMapper);

    public BarcodeCameraPreviewHandler()
        : base(Mapper)
    {
    }

    protected override TextureView CreatePlatformView() => new(Context)
    {
        LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent)
    };
}
