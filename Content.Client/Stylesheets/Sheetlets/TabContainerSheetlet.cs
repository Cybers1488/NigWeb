using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class TabContainerSheetlet<T> : Sheetlet<T> where T: PalettedStylesheet, ITabContainerConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        ITabContainerConfig tabCfg = sheet;

        var tabContainerPanel = sheet.GetTextureOr(tabCfg.TabContainerPanelPath, NanotrasenStylesheet.TextureRoot)
            .IntoPatch(StyleBox.Margin.All, 2);

        var tabCustomTex = sheet.GetTextureOr(new Robust.Shared.Utility.ResPath("goob_button.png"), NanotrasenStylesheet.TextureRoot);
        var tabContainerBoxActive = new StyleBoxTexture
        {
            Texture = tabCustomTex,
            Modulate = Color.White
        };
        tabContainerBoxActive.SetPatchMargin(StyleBox.Margin.All, 4);
        tabContainerBoxActive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

        var tabContainerBoxInactive = new StyleBoxTexture
        {
            Texture = tabCustomTex,
            Modulate = new Color(150, 150, 150)
        };
        tabContainerBoxInactive.SetPatchMargin(StyleBox.Margin.All, 4);
        tabContainerBoxInactive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

        return
        [
            E<TabContainer>()
                .Prop(TabContainer.StylePropertyPanelStyleBox, tabContainerPanel)
                .Prop(TabContainer.StylePropertyTabStyleBox, tabContainerBoxActive)
                .Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabContainerBoxInactive),
        ];
    }
}
