using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class ScrollbarSheetlet : Sheetlet<PalettedStylesheet>
{
    public const int DefaultGrabberSize = 10;

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var resCache = IoCManager.Resolve<IResourceCache>();

        // Vertical Grabber
        var vGrabberTexNormal = resCache.GetResource<TextureResource>("/Textures/Interface/Nano/lfwb_scrollbar_grabber.png").Texture;
        var vScrollBarGrabberNormal = new StyleBoxTexture
        {
            Texture = vGrabberTexNormal,
            Mode = StyleBoxTexture.StretchMode.Stretch,
            ContentMarginLeftOverride = 14,
            ContentMarginTopOverride = DefaultGrabberSize,
        };
        vScrollBarGrabberNormal.SetPatchMargin(StyleBox.Margin.Top, 4);
        vScrollBarGrabberNormal.SetPatchMargin(StyleBox.Margin.Bottom, 4);

        var vScrollBarGrabberHover = new StyleBoxTexture
        {
            Texture = vGrabberTexNormal,
            Mode = StyleBoxTexture.StretchMode.Stretch,
            Modulate = new Color(200, 200, 200),
            ContentMarginLeftOverride = 14,
            ContentMarginTopOverride = DefaultGrabberSize,
        };
        vScrollBarGrabberHover.SetPatchMargin(StyleBox.Margin.Top, 4);
        vScrollBarGrabberHover.SetPatchMargin(StyleBox.Margin.Bottom, 4);

        var vScrollBarGrabberGrabbed = new StyleBoxTexture
        {
            Texture = vGrabberTexNormal,
            Mode = StyleBoxTexture.StretchMode.Stretch,
            Modulate = new Color(150, 150, 150),
            ContentMarginLeftOverride = 14,
            ContentMarginTopOverride = DefaultGrabberSize,
        };
        vScrollBarGrabberGrabbed.SetPatchMargin(StyleBox.Margin.Top, 4);
        vScrollBarGrabberGrabbed.SetPatchMargin(StyleBox.Margin.Bottom, 4);

        // Horizontal Grabber
        var hGrabberTexNormal = resCache.GetResource<TextureResource>("/Textures/Interface/Nano/lfwb_scrollbar_grabber_h.png").Texture;
        var hScrollBarGrabberNormal = new StyleBoxTexture
        {
            Texture = hGrabberTexNormal,
            Mode = StyleBoxTexture.StretchMode.Stretch,
            ContentMarginTopOverride = 14,
            ContentMarginLeftOverride = DefaultGrabberSize,
        };
        hScrollBarGrabberNormal.SetPatchMargin(StyleBox.Margin.Left, 4);
        hScrollBarGrabberNormal.SetPatchMargin(StyleBox.Margin.Right, 4);

        var hScrollBarGrabberHover = new StyleBoxTexture
        {
            Texture = hGrabberTexNormal,
            Mode = StyleBoxTexture.StretchMode.Stretch,
            Modulate = new Color(200, 200, 200),
            ContentMarginTopOverride = 14,
            ContentMarginLeftOverride = DefaultGrabberSize,
        };
        hScrollBarGrabberHover.SetPatchMargin(StyleBox.Margin.Left, 4);
        hScrollBarGrabberHover.SetPatchMargin(StyleBox.Margin.Right, 4);

        var hScrollBarGrabberGrabbed = new StyleBoxTexture
        {
            Texture = hGrabberTexNormal,
            Mode = StyleBoxTexture.StretchMode.Stretch,
            Modulate = new Color(150, 150, 150),
            ContentMarginTopOverride = 14,
            ContentMarginLeftOverride = DefaultGrabberSize,
        };
        hScrollBarGrabberGrabbed.SetPatchMargin(StyleBox.Margin.Left, 4);
        hScrollBarGrabberGrabbed.SetPatchMargin(StyleBox.Margin.Right, 4);

        return
        [
            E<VScrollBar>().Prop(ScrollBar.StylePropertyGrabber, vScrollBarGrabberNormal),
            E<VScrollBar>().PseudoHovered().Prop(ScrollBar.StylePropertyGrabber, vScrollBarGrabberHover),
            E<VScrollBar>().PseudoPressed().Prop(ScrollBar.StylePropertyGrabber, vScrollBarGrabberGrabbed),
            E<HScrollBar>().Prop(ScrollBar.StylePropertyGrabber, hScrollBarGrabberNormal),
            E<HScrollBar>().PseudoHovered().Prop(ScrollBar.StylePropertyGrabber, hScrollBarGrabberHover),
            E<HScrollBar>().PseudoPressed().Prop(ScrollBar.StylePropertyGrabber, hScrollBarGrabberGrabbed),
        ];
    }
}
