# -*- coding: utf-8 -*-
import re

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'r', encoding='utf-8') as f:
    content = f.read()

patch = """    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _isBody = _entities.HasComponent<BodyComponent>(_part);
        _window.Surgeries.DisposeAllChildren();

        // NigWeb: Teeth UI Custom Injection
        if (_entities.TryGetComponent(_part, out BodyPartComponent? partComp) && partComp.PartType == BodyPartType.Head)
        {
            if (_entities.TryGetComponent(Owner, out Content.Shared._NigWeb.Teeth.TeethComponent? teethComp))
            {
                var label = new Robust.Client.UserInterface.Controls.RichTextLabel();
                var msg = new FormattedMessage();
                msg.AddMarkup($"[bold][color=orange]Рот (Осталось зубов: {teethComp.CurrentTeeth})[/color][/bold]");
                label.SetMessage(msg);
                label.Margin = new Robust.Client.Graphics.Thickness(0, 0, 0, 5);
                
                _window.Surgeries.AddChild(label);
                _window.Surgeries.AddChild(new Content.Client.Administration.UI.CustomControls.HSeparator { Margin = new Robust.Client.Graphics.Thickness(0, 0, 0, 5) });
            }
        }
"""

if "Рот (Осталось зубов" not in content:
    content = content.replace('''    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _isBody = _entities.HasComponent<BodyComponent>(_part);
        _window.Surgeries.DisposeAllChildren();''', patch)

with open('Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs', 'w', encoding='utf-8') as f:
    f.write(content)
