// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.Shared.Hands.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Hands.Controls;

public sealed class HandsContainer : ItemSlotUIContainer<HandButton>
{
    private readonly GridContainer _grid;
    public int ColumnLimit { get => _grid.Columns; set => _grid.Columns = value; }
    public int MaxButtonCount { get; set; } = 0;

    public int MaxButtonsPerRow { get; set;  }= 6;

    /// <summary>
    ///     Indexer. This is used to reference a HandsContainer from the
    ///     controller.
    /// </summary>
    public string? Indexer { get; set; }

    private float _slotSize = 96f;

    public TextureButton? SwapButton { get; private set; }
    public TextureButton? LeftPillar { get; private set; }
    public TextureButton? RightPillar { get; private set; }
    private bool _lastIsLeftActive;

    private TextureButton CreateSwapButton()
    {
        return new TextureButton
        {
            MinSize = new System.Numerics.Vector2(_slotSize, _slotSize),
            SetSize = new System.Numerics.Vector2(_slotSize, _slotSize),
            MouseFilter = MouseFilterMode.Ignore,
        };
    }

    private TextureButton CreatePillar(string textureName)
    {
        var btn = new TextureButton
        {
            MinSize = new System.Numerics.Vector2(_slotSize, _slotSize),
            SetSize = new System.Numerics.Vector2(_slotSize, _slotSize),
            MouseFilter = MouseFilterMode.Ignore,
        };

        var resCache = IoCManager.Resolve<IResourceCache>();
        if (resCache.TryGetResource<TextureResource>(new ResPath($"/Textures/Interface/NigWeb/{textureName}"), out var res) ||
            resCache.TryGetResource<TextureResource>(new ResPath($"/Textures/Interface/Default/{textureName}"), out res))
        {
            btn.TextureNormal = res.Texture;
        }

        return btn;
    }

    public void UpdateSwapButton(bool isLeftActive)
    {
        _lastIsLeftActive = isLeftActive;
        if (SwapButton == null)
            return;

        var resCache = IoCManager.Resolve<IResourceCache>();
        var texName = isLeftActive ? "swap_hand_left.png" : "swap_hand_right.png";
        if (resCache.TryGetResource<TextureResource>(new ResPath($"/Textures/Interface/NigWeb/{texName}"), out var res) ||
            resCache.TryGetResource<TextureResource>(new ResPath($"/Textures/Interface/Default/{texName}"), out res))
        {
            SwapButton.TextureNormal = res.Texture;
        }
    }

    public HandsContainer()
    {
        HSeparationOverride = 0;
        VSeparationOverride = 0;
        ExpandBackwards = false;
        AddChild(_grid = new GridContainer
        {
            HSeparationOverride = 0,
            VSeparationOverride = 0,
            ExpandBackwards = false,
        });
        _grid.ExpandBackwards = false;
    }

    public void SetSlotSize(float slotSize)
    {
        _slotSize = slotSize;
        _grid.HSeparationOverride = 0;
        _grid.VSeparationOverride = 0;
        if (ColumnLimit == 3)
        {
            MinSize = new System.Numerics.Vector2(slotSize * 3f, slotSize * 2f);
            SetSize = new System.Numerics.Vector2(slotSize * 3f, slotSize * 2f);
        }
        else
        {
            MinSize = new System.Numerics.Vector2(slotSize * 3f, slotSize);
            SetSize = new System.Numerics.Vector2(slotSize * 3f, slotSize);
        }

        foreach (var child in _grid.Children)
        {
            if (child is HandButton button)
            {
                button.SetButtonSize(slotSize);
            }
            else if (child is TextureButton texBtn)
            {
                texBtn.MinSize = new System.Numerics.Vector2(slotSize, slotSize);
                texBtn.SetSize = new System.Numerics.Vector2(slotSize, slotSize);
            }
            else
            {
                child.MinSize = new System.Numerics.Vector2(slotSize, slotSize);
                child.SetSize = new System.Numerics.Vector2(slotSize, slotSize);
            }
        }
    }

    public override HandButton? AddButton(HandButton newButton)
    {
        if (ColumnLimit == 3)
        {
            _grid.Columns = 3;
            _grid.ExpandBackwards = false;
            while (_grid.ChildCount < 6)
            {
                _grid.AddChild(new Control
                {
                    MinSize = new System.Numerics.Vector2(_slotSize, _slotSize),
                    SetSize = new System.Numerics.Vector2(_slotSize, _slotSize),
                    MouseFilter = MouseFilterMode.Ignore
                });
            }

            if (LeftPillar == null)
            {
                LeftPillar = CreatePillar("hud_pillar_l.png");
                _grid.RemoveChild(_grid.GetChild(3));
                _grid.AddChild(LeftPillar);
                LeftPillar.SetPositionInParent(3);
            }

            if (SwapButton == null)
            {
                SwapButton = CreateSwapButton();
                _grid.RemoveChild(_grid.GetChild(4));
                _grid.AddChild(SwapButton);
                SwapButton.SetPositionInParent(4);
                UpdateSwapButton(_lastIsLeftActive);
            }

            if (RightPillar == null)
            {
                RightPillar = CreatePillar("hud_pillar_r.png");
                _grid.RemoveChild(_grid.GetChild(5));
                _grid.AddChild(RightPillar);
                RightPillar.SetPositionInParent(5);
            }

            newButton.SetButtonSize(_slotSize);
            var targetIndex = newButton.HandLocation == HandLocation.Right ? 0 : 2;
            var oldChild = _grid.GetChild(targetIndex);
            _grid.RemoveChild(oldChild);
            _grid.AddChild(newButton);
            newButton.SetPositionInParent(targetIndex);
            return base.AddButton(newButton);
        }

        if (MaxButtonCount > 0)
        {
            if (ButtonCount >= MaxButtonCount)
                return null;

            _grid.AddChild(newButton);
        }
        else
        {
            _grid.AddChild(newButton);
        }

        _grid.Columns = Math.Min(_grid.ChildCount, MaxButtonsPerRow);
        return base.AddButton(newButton);
    }

    public override void RemoveButton(string handName)
    {
        var button = GetButton(handName);
        if (button == null)
            return;
        base.RemoveButton(button);

        if (ColumnLimit == 3)
        {
            var idx = -1;
            for (var i = 0; i < _grid.ChildCount; i++)
            {
                if (_grid.GetChild(i) == button)
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                _grid.RemoveChild(button);
                var placeholder = new Control
                {
                    MinSize = new System.Numerics.Vector2(_slotSize, _slotSize),
                    SetSize = new System.Numerics.Vector2(_slotSize, _slotSize),
                    MouseFilter = MouseFilterMode.Ignore
                };
                _grid.AddChild(placeholder);
                placeholder.SetPositionInParent(idx);
            }
            return;
        }

        _grid.RemoveChild(button);
    }

    public bool TryGetLastButton(out HandButton? control)
    {
        if (Buttons.Count == 0)
        {
            control = null;
            return false;
        }

        control = Buttons.Values.Last();
        return true;
    }

    public bool TryRemoveLastHand(out HandButton? control)
    {
        var success = TryGetLastButton(out control);
        if (control != null)
            RemoveButton(control);
        return success;
    }

    public void Clear()
    {
        ClearButtons();
        _grid.RemoveAllChildren();
        SwapButton = null;
        LeftPillar = null;
        RightPillar = null;
    }

    public IEnumerable<HandButton> GetButtons()
    {
        foreach (var child in _grid.Children)
        {
            if (child is HandButton hand)
                yield return hand;
        }
    }

    public bool IsFull => (MaxButtonCount != 0 && ButtonCount >= MaxButtonCount);

    public int ButtonCount => Buttons.Count;
}
