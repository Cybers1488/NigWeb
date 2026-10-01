using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Client.UserInterface.Controls;
using Content.Goobstation.UIKit.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets.Hud;

[CommonSheetlet]
public sealed class ChatSheetlet<T> : Sheetlet<T> where T: PalettedStylesheet, IButtonConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig btnCfg = sheet;

        // Goobstation - Custom chat texture (Lifeweb style)
        var chatPanelTex = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>("/Textures/Interface/Nano/lfwb_chat_panel.png").Texture;
        var chatBg = new StyleBoxTexture
        {
            Texture = chatPanelTex,
            Mode = StyleBoxTexture.StretchMode.Tile,
        };

        var chatChannelButtonTex =
            sheet.GetTextureOr(btnCfg.RoundedButtonBorderedPath, NanotrasenStylesheet.TextureRoot);
        var chatChannelButton = new StyleBoxTexture
        {
            Texture = chatChannelButtonTex,
        };
        chatChannelButton.SetPatchMargin(StyleBox.Margin.All, 5);
        chatChannelButton.SetPadding(StyleBox.Margin.All, 2);

        var chatFilterButtonTex =
            sheet.GetTextureOr(btnCfg.RoundedButtonBorderedPath, NanotrasenStylesheet.TextureRoot);
        var chatFilterButton = new StyleBoxTexture
        {
            Texture = chatFilterButtonTex,
        };
        chatFilterButton.SetPatchMargin(StyleBox.Margin.All, 5);
        chatFilterButton.SetPadding(StyleBox.Margin.All, 2);

        return
        [
            E<PanelContainer>()
                .Class(ChatInputBox.StyleClassChatPanel)
                .Panel(chatBg),
            E<CustomOutputPanel>()
                .Class("ChatOutput")
                .Prop(CustomOutputPanel.StylePropertyStyleBox, chatBg),
            E<LineEdit>()
                .Class(ChatInputBox.StyleClassChatLineEdit)
                .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty()),
            E<Button>().Class(ChatInputBox.StyleClassChatFilterOptionButton).Box(chatChannelButton),
            E<ContainerButton>().Class(ChatInputBox.StyleClassChatFilterOptionButton).Box(chatFilterButton),
        ];
    }
}
