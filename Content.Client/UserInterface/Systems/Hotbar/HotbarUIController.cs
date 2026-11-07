// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hands;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Inventory;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Client.UserInterface.Systems.Storage;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.UserInterface.Systems.Hotbar;

public sealed class HotbarUIController : UIController
{
    private InventoryUIController? _inventory;
    private HandsUIController? _hands;
    private StorageUIController? _storage;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
    }

    private void OnScreenLoad()
    {
        ReloadHotbar();
    }

    public void Setup(HandsContainer handsContainer)
    {
        _inventory = UIManager.GetUIController<InventoryUIController>();
        _hands = UIManager.GetUIController<HandsUIController>();
        _storage = UIManager.GetUIController<StorageUIController>();
        _hands.RegisterHandContainer(handsContainer);
    }

    public void ReloadHotbar()
    {
        if (UIManager.ActiveScreen == null)
        {
            return;
        }

        if (UIManager.ActiveScreen.GetWidget<HotbarGui>() is { } hotbar)
        {
            foreach (var container in GetAllItemSlotContainers(hotbar))
            {
                container.SlotGroup = container.SlotGroup;
            }
        }

        var inventory = UIManager.ActiveScreen.GetWidget<InventoryGui>();
        if (inventory != null)
        {
            foreach (var container in GetAllItemSlotContainers(inventory))
            {
                container.SlotGroup = container.SlotGroup;
            }

            _inventory?.RegisterInventoryBarContainer(inventory.InventoryHotbar);
        }

        _hands?.ReloadHands();
        _inventory?.ReloadSlots();
    }

    private static IEnumerable<ItemSlotButtonContainer> GetAllItemSlotContainers(Control gui)
    {
        var result = new List<ItemSlotButtonContainer>();

        foreach (var child in gui.Children)
        {
            if (child is ItemSlotButtonContainer container)
            {
                result.Add(container);
            }

            result.AddRange(GetAllItemSlotContainers(child));
        }

        return result;
    }
}
