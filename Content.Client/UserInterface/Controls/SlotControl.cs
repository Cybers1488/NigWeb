// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.Cooldown;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Controls
{
    [Virtual]
    public abstract class SlotControl : Control, IEntityControl
    {
        public static int DefaultButtonSize = 64;

        public TextureRect ButtonRect { get; }
        public TextureRect FillAnimRect { get; }
        public TextureRect BlockedRect { get; }
        public TextureRect HighlightRect { get; }
        public SpriteView HoverSpriteView { get; }
        public TextureButton StorageButton { get; }
        public CooldownGraphic CooldownDisplay { get; }

        private SpriteView SpriteView { get; }
        private EntityPrototypeView ProtoView { get; }

        public EntityUid? Entity => SpriteView.Entity;

        private bool _slotNameSet;

        private string _slotName = "";
        public string SlotName
        {
            get => _slotName;
            set
            {
                //this auto registers the button with it's parent container when it's set
                if (_slotNameSet)
                {
                    Logger.Warning("Tried to set slotName after init for:" + Name);
                    return;
                }
                _slotNameSet = true;
                if (Parent is IItemslotUIContainer container)
                {
                    container.TryRegisterButton(this, value);
                }
                Name = "SlotButton_" + value;
                _slotName = value;
            }
        }

        private float _highlightAnimTimer;
        private float _nextGlitchInterval = 0.08f;
        private int _highlightFrameIndex;
        private Texture[]? _cachedHighlightFrames;

        private bool _fillAnimActive;
        private bool _fillAnimLoop;
        private float _fillAnimTimer;
        private int _fillAnimFrame;
        private Texture[]? _cachedFillFrames;

        public bool Highlight
        {
            get => HighlightRect.Visible;
            set
            {
                if (HighlightRect.Visible == value)
                    return;

                HighlightRect.Visible = value;
                if (value)
                {
                    _highlightAnimTimer = 0f;
                    _nextGlitchInterval = 0.04f + Random.Shared.NextSingle() * 0.10f;
                    if (_cachedHighlightFrames == null || _cachedHighlightFrames.Length == 0)
                    {
                        LoadHighlightFrames();
                    }
                    else
                    {
                        _highlightFrameIndex = Random.Shared.Next(_cachedHighlightFrames.Length);
                        HighlightRect.Texture = _cachedHighlightFrames[_highlightFrameIndex];
                    }
                }
            }
        }

        public bool Blocked { get => BlockedRect.Visible; set => BlockedRect.Visible = value;}

        private string? _blockedTexturePath;
        public string? BlockedTexturePath
        {
            get => _blockedTexturePath;
            set
            {
                _blockedTexturePath = value;
                BlockedRect.Texture = Theme.ResolveTextureOrNull(_blockedTexturePath)?.Texture;
            }
        }

        private string? _buttonTexturePath;
        public string? ButtonTexturePath
        {
            get => _buttonTexturePath;
            set
            {
                _buttonTexturePath = value;
                UpdateButtonTexture();
            }
        }

        private string? _fullButtonTexturePath;
        public string? FullButtonTexturePath
        {
            get => _fullButtonTexturePath;
            set
            {
                _fullButtonTexturePath = value;
                UpdateButtonTexture();
            }
        }


        private string? _storageTexturePath;
        public string? StorageTexturePath
        {
            get => _buttonTexturePath;
            set
            {
                _storageTexturePath = value;
                StorageButton.TextureNormal = Theme.ResolveTextureOrNull(_storageTexturePath)?.Texture;
            }
        }

        private string? _highlightTexturePath;
        public string? HighlightTexturePath
        {
            get => _highlightTexturePath;
            set
            {
                _highlightTexturePath = value;
                LoadHighlightFrames();
            }
        }

        private void LoadHighlightFrames()
        {
            var resCache = IoCManager.Resolve<IResourceCache>();
            var frames = new List<Texture>();
            for (var i = 0; i < 3; i++)
            {
                var path = new ResPath($"/Textures/Interface/NigWeb/sel_hand_{i}.png");
                if (resCache.TryGetResource<TextureResource>(path, out var res))
                {
                    frames.Add(res.Texture);
                }
                else
                {
                    var fallback = new ResPath($"/Textures/Interface/Default/sel_hand_{i}.png");
                    if (resCache.TryGetResource<TextureResource>(fallback, out var fallbackRes))
                        frames.Add(fallbackRes.Texture);
                }
            }

            if (frames.Count == 0 && _highlightTexturePath != null)
            {
                var fallback = Theme.ResolveTextureOrNull(_highlightTexturePath);
                if (fallback?.Texture != null)
                    frames.Add(fallback.Texture);
            }

            _cachedHighlightFrames = frames.Count > 0 ? frames.ToArray() : null;
            if (_cachedHighlightFrames != null && _cachedHighlightFrames.Length > 0)
            {
                _highlightFrameIndex = Math.Clamp(_highlightFrameIndex, 0, _cachedHighlightFrames.Length - 1);
                HighlightRect.Texture = _cachedHighlightFrames[_highlightFrameIndex];
            }
            else
            {
                HighlightRect.Texture = Theme.ResolveTextureOrNull(_highlightTexturePath)?.Texture;
            }
        }

        public event Action<GUIBoundKeyEventArgs, SlotControl>? Pressed;
        public event Action<GUIBoundKeyEventArgs, SlotControl>? Unpressed;
        public event Action<GUIBoundKeyEventArgs, SlotControl>? StoragePressed;
        public event Action<GUIMouseHoverEventArgs, SlotControl>? Hover;

        public bool EntityHover => HoverSpriteView.Sprite != null;
        public bool MouseIsHovering;

        public SlotControl()
        {
            IoCManager.InjectDependencies(this);
            Name = "SlotButton_null";
            MinSize = new Vector2(DefaultButtonSize, DefaultButtonSize);
            AddChild(ButtonRect = new TextureRect
            {
                TextureScale = new Vector2(2, 2),
                MouseFilter = MouseFilterMode.Stop
            });
            AddChild(FillAnimRect = new TextureRect
            {
                Visible = false,
                TextureScale = new Vector2(2, 2),
                MouseFilter = MouseFilterMode.Ignore
            });
            AddChild(HighlightRect = new TextureRect
            {
                Visible = false,
                TextureScale = new Vector2(2, 2),
                MouseFilter = MouseFilterMode.Ignore
            });

            ButtonRect.OnKeyBindDown += OnButtonPressed;
            ButtonRect.OnKeyBindUp += OnButtonUnpressed;

            AddChild(SpriteView = new SpriteView
            {
                Scale = new Vector2(2, 2),
                SetSize = new Vector2(DefaultButtonSize, DefaultButtonSize),
                OverrideDirection = Direction.South
            });
            AddChild(ProtoView = new EntityPrototypeView
            {
                Visible = false,
                Scale = new Vector2(2, 2),
                SetSize = new Vector2(DefaultButtonSize, DefaultButtonSize),
                OverrideDirection = Direction.South
            });

            AddChild(HoverSpriteView = new SpriteView
            {
                Scale = new Vector2(2, 2),
                SetSize = new Vector2(DefaultButtonSize, DefaultButtonSize),
                OverrideDirection = Direction.South
            });

            AddChild(StorageButton = new TextureButton
            {
                Scale = new Vector2(0.75f, 0.75f),
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Bottom,
                Visible = false,
            });

            StorageButton.OnKeyBindDown += args =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                {
                    OnButtonPressed(args);
                }
            };

            StorageButton.OnPressed += OnStorageButtonPressed;

            ButtonRect.OnMouseEntered += _ =>
            {
                MouseIsHovering = true;
            };
            ButtonRect.OnMouseEntered += OnButtonHover;

            ButtonRect.OnMouseExited += _ =>
            {
                MouseIsHovering = false;
                ClearHover();
            };

            AddChild(CooldownDisplay = new CooldownGraphic
            {
                Visible = false,
            });

            AddChild(BlockedRect = new TextureRect
            {
                TextureScale = new Vector2(2, 2),
                MouseFilter = MouseFilterMode.Stop,
                Visible = false
            });

            HighlightTexturePath = "slot_highlight";
            BlockedTexturePath = "blocked";
        }

        public void SetButtonSize(float size)
        {
            MinSize = new Vector2(size, size);
            SetSize = new Vector2(size, size);
            var scale = size / 32f;
            ButtonRect.TextureScale = new Vector2(scale, scale);
            FillAnimRect.TextureScale = new Vector2(scale, scale);
            HighlightRect.TextureScale = new Vector2(scale, scale);
            BlockedRect.TextureScale = new Vector2(scale, scale);
            SpriteView.Scale = new Vector2(scale, scale);
            SpriteView.SetSize = new Vector2(size, size);
            ProtoView.Scale = new Vector2(scale, scale);
            ProtoView.SetSize = new Vector2(size, size);
            HoverSpriteView.Scale = new Vector2(scale, scale);
            HoverSpriteView.SetSize = new Vector2(size, size);
            StorageButton.Scale = new Vector2(scale * 0.375f, scale * 0.375f);
        }

        public void ClearHover()
        {
            if (!EntityHover)
                return;

            var tempQualifier = HoverSpriteView.Entity;
            if (tempQualifier != null)
            {
                IoCManager.Resolve<IEntityManager>().QueueDeleteEntity(tempQualifier);
            }

            HoverSpriteView.SetEntity(null);
        }

        /// <summary>
        /// Causes the control to display a placeholder prototype, optionally faded
        /// </summary>
        public void SetEntity(EntityUid? ent)
        {
            if (ent != null && ent != Entity)
                PlayFillAnimation();

            SpriteView.SetEntity(ent);
            SpriteView.Visible = true;
            ProtoView.Visible = false;
            UpdateButtonTexture();
        }

        /// <summary>
        /// Causes the control to display a placeholder prototype, optionally faded
        /// </summary>
        public void SetPrototype(EntProtoId? proto, bool fade)
        {
            ProtoView.SetPrototype(proto);
            SpriteView.Visible = false;
            ProtoView.Visible = true;

            UpdateButtonTexture();

            if (ProtoView.Entity is not { } ent || !fade)
                return;

            var sprites = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
            sprites.SetColor((ent.Owner, ent.Comp1), Color.DarkGray.WithAlpha(0.65f));
        }

        private void UpdateButtonTexture()
        {
            var texture = Theme.ResolveTextureOrNull(_buttonTexturePath)?.Texture
                ?? Theme.ResolveTextureOrNull(_fullButtonTexturePath)?.Texture;
            ButtonRect.Texture = texture;
        }

        private void OnButtonPressed(GUIBoundKeyEventArgs args)
        {
            if (!Entity.HasValue)
                PlayFillAnimation();

            Pressed?.Invoke(args, this);
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();
            PlayFillAnimation();
        }

        private void OnButtonUnpressed(GUIBoundKeyEventArgs args)
        {
            Unpressed?.Invoke(args, this);
        }

        private void OnStorageButtonPressed(BaseButton.ButtonEventArgs args)
        {
            if (args.Event.Function == EngineKeyFunctions.UIClick)
            {
                StoragePressed?.Invoke(args.Event, this);
            }
            else
            {
                Pressed?.Invoke(args.Event, this);
            }
        }

        private void OnButtonHover(GUIMouseHoverEventArgs args)
        {
            Hover?.Invoke(args, this);
        }

        public void PlayFillAnimation(bool loop = false)
        {
            if (_cachedFillFrames == null || _cachedFillFrames.Length == 0)
                LoadFillFrames();

            if (_cachedFillFrames == null || _cachedFillFrames.Length == 0)
                return;

            _fillAnimActive = true;
            _fillAnimLoop = loop;
            _fillAnimTimer = 0f;
            _fillAnimFrame = 0;
            FillAnimRect.Texture = _cachedFillFrames[0];
            FillAnimRect.Visible = true;
        }

        private void LoadFillFrames()
        {
            var resCache = IoCManager.Resolve<IResourceCache>();
            var fillFrames = new List<Texture>();
            for (var i = 0; i < 16; i++)
            {
                var path = new ResPath($"/Textures/Interface/NigWeb/hud_fill_{i}.png");
                if (resCache.TryGetResource<TextureResource>(path, out var res))
                {
                    fillFrames.Add(res.Texture);
                }
                else
                {
                    var fallback = new ResPath($"/Textures/Interface/Default/hud_fill_{i}.png");
                    if (resCache.TryGetResource<TextureResource>(fallback, out var fallbackRes))
                        fillFrames.Add(fallbackRes.Texture);
                    else
                        break;
                }
            }

            _cachedFillFrames = fillFrames.Count > 0 ? fillFrames.ToArray() : null;
        }

        protected override void OnThemeUpdated()
        {
            base.OnThemeUpdated();

            StorageButton.TextureNormal = Theme.ResolveTextureOrNull(_storageTexturePath)?.Texture;
            LoadHighlightFrames();
            LoadFillFrames();
            UpdateButtonTexture();
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_fillAnimActive && _cachedFillFrames != null && _cachedFillFrames.Length > 0)
            {
                _fillAnimTimer += args.DeltaSeconds;
                if (_fillAnimTimer >= 0.06f)
                {
                    _fillAnimTimer -= 0.06f;
                    _fillAnimFrame++;
                    if (_fillAnimFrame >= _cachedFillFrames.Length)
                    {
                        if (_fillAnimLoop)
                        {
                            _fillAnimFrame = 0;
                            FillAnimRect.Texture = _cachedFillFrames[_fillAnimFrame];
                        }
                        else
                        {
                            _fillAnimActive = false;
                            FillAnimRect.Visible = false;
                        }
                    }
                    else
                    {
                        FillAnimRect.Texture = _cachedFillFrames[_fillAnimFrame];
                    }
                }
            }

            if (!HighlightRect.Visible)
                return;

            if (_cachedHighlightFrames == null || _cachedHighlightFrames.Length <= 1)
                return;

            _highlightAnimTimer += args.DeltaSeconds;
            if (_highlightAnimTimer >= _nextGlitchInterval)
            {
                _highlightAnimTimer = 0f;
                _nextGlitchInterval = 0.04f + Random.Shared.NextSingle() * 0.12f;

                var nextFrame = Random.Shared.Next(_cachedHighlightFrames.Length - 1);
                if (nextFrame >= _highlightFrameIndex)
                    nextFrame++;

                _highlightFrameIndex = nextFrame;
                HighlightRect.Texture = _cachedHighlightFrames[_highlightFrameIndex];
            }
        }

        EntityUid? IEntityControl.UiEntity => Entity;
    }
}