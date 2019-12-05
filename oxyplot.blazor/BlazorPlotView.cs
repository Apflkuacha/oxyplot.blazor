﻿using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace OxyPlot.Blazor
{
    public class BlazorPlotView : ComponentBase, IPlotView, IDisposable
    {
        [Inject] IJSRuntime JSRuntime { get; set; }
        [Parameter] public string Width { get; set; }
        [Parameter] public string Height { get; set; }
        [Parameter]
        public bool TrackerEnabled
        {
            get => _trackerEnabled;
            set
            {
                if (_trackerEnabled != value)
                {
                    _trackerEnabled = value;
                    if (_trackerEnabled)
                    {
                        StateHasChanged();
                    }
                }
            }
        }
        /// <summary>
        /// Gets or sets the plot controller.
        /// </summary>
        /// <value>The controller.</value>
        [Parameter] public IPlotController Controller { get; set; }
        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        [Parameter]
        public PlotModel Model
        {
            get => model;
            set
            {
                if (this.model != value)
                {
                    this.model = value;
                    this.OnModelChanged();
                }
            }
        }

        

        private DotNetObjectReference<BlazorPlotView> _self;
        private ElementReference _svg;
        private TrackerHitResult _tracker;
        private bool _trackerEnabled;
        private OxyRect _svgPos = new OxyRect(0, 0, 0, 0);

        [JSInvokable]
        public void UpdateSvgBoundingRect(double[] pos)
        {
            var n = new OxyRect(pos[0], pos[1], pos[2], pos[3]);
            // OxyRect.Equals is very picky
            if (false
                || Math.Abs(n.Left - _svgPos.Left) > 0.5
                || Math.Abs(n.Top - _svgPos.Top) > 0.5
                || Math.Abs(n.Width - _svgPos.Width) > 0.5
                || Math.Abs(n.Height - _svgPos.Height) > 0.5
                )
            {
                _svgPos = n;
                StateHasChanged();
            }
        }
        /// <summary>
        /// The current model (holding a reference to this plot view).
        /// </summary>
        private PlotModel currentModel;

        /// <summary>
        /// The model.
        /// </summary>
        private PlotModel model;

        /// <summary>
        /// The default controller.
        /// </summary>
        private IPlotController defaultController;

        /// <summary>
        /// The update data flag.
        /// </summary>
        private bool updateDataFlag = true;

        /// <summary>
        /// The zoom rectangle.
        /// </summary>
        private OxyRect zoomRectangle;

        /// <summary>
        /// Gets the actual model in the view.
        /// </summary>
        /// <value>
        /// The actual model.
        /// </value>
        Model IView.ActualModel => Model;

        /// <summary>
        /// Gets the actual model.
        /// </summary>
        /// <value>The actual model.</value>
        public PlotModel ActualModel => Model;

        /// <summary>
        /// Gets the actual controller.
        /// </summary>
        /// <value>
        /// The actual <see cref="IController" />.
        /// </value>
        IController IView.ActualController => ActualController;

        /// <summary>
        /// Gets the coordinates of the client area of the view.
        /// </summary>
        public OxyRect ClientArea => _svgPos;

        /// <summary>
        /// Gets the actual plot controller.
        /// </summary>
        /// <value>The actual plot controller.</value>
        public IPlotController ActualController => this.Controller ?? (this.defaultController ?? (this.defaultController = new PlotController()));

        /// <summary>
        /// Shows the tracker.
        /// </summary>
        /// <param name="data">The data.</param>
        public void ShowTracker(TrackerHitResult data)
        {
            if (_tracker != data)
            {
                _tracker = data;
                StateHasChanged();
            }
            StateHasChanged();
        }

        /// <summary>
        /// Hides the tracker.
        /// </summary>
        public void HideTracker()
        {
            ShowTracker(null);
        }

        /// <summary>
        /// Hides the zoom rectangle.
        /// </summary>
        public void HideZoomRectangle()
        {
            this.zoomRectangle = OxyRect.Create(0, 0, 0, 0);
            this.Invalidate();
        }

        protected void Invalidate()
        {
            StateHasChanged();
        }

        /// <summary>
        /// Invalidates the plot (not blocking the UI thread)
        /// </summary>
        /// <param name="updateData">if set to <c>true</c>, all data collections will be updated.</param>
        public void InvalidatePlot(bool updateData)
        {
            this.updateDataFlag |= updateData;
            this.Invalidate();
        }

        /// <summary>
        /// Called when the Model property has been changed.
        /// </summary>
        public void OnModelChanged()
        {
            if (this.currentModel != null)
            {
                ((IPlotModel)this.currentModel).AttachPlotView(null);
                this.currentModel = null;
            }

            if (this.Model != null)
            {
                ((IPlotModel)this.Model).AttachPlotView(this);
                this.currentModel = this.Model;

                this.InvalidatePlot(true);
            }
        }

        /// <summary>
        /// Sets the cursor type.
        /// </summary>
        /// <param name="cursorType">The cursor type.</param>
        public void SetCursorType(CursorType cursorType)
            => _svg.SetCursor(JSRuntime, TranslateCursorType(cursorType));

        /// <summary>
        /// Shows the zoom rectangle.
        /// </summary>
        /// <param name="rectangle">The rectangle.</param>
        public void ShowZoomRectangle(OxyRect rectangle)
        {
            this.zoomRectangle = rectangle;
            this.Invalidate();
        }

        /// <summary>
        /// Sets the clipboard text.
        /// </summary>
        /// <param name="text">The text.</param>
        public void SetClipboardText(string text)
        {
            // todo: not tested yet, don't know when it is called
            // https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText
            JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }

        void AddEventCallback<T>(RenderTreeBuilder builder, int sequence, string name, Action<T> callback)
        {
            builder.AddEventPreventDefaultAttribute(sequence, name, true);
            builder.AddEventStopPropagationAttribute(sequence, name, true);
            builder.AddAttribute(sequence, name, EventCallback.Factory.Create<T>(this, callback));
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            // note this gist about seequence numbers
            // https://gist.github.com/SteveSandersonMS/ec232992c2446ab9a0059dd0fbc5d0c3
            builder.OpenElement(0, "svg");
            if (!String.IsNullOrEmpty(Width))
            {
                builder.AddAttribute(1, "width", Width);
            }
            if (!String.IsNullOrEmpty(Height))
            {
                builder.AddAttribute(2, "height", Height);
            }
            if (_svgPos.Width > 0)
            {
                builder.AddAttribute(1, "viewBox", FormattableString.Invariant($"0 0 {_svgPos.Width} {_svgPos.Height}"));
                // available event handlers
                // https://github.com/aspnet/AspNetCore/blob/master/src/Components/Web/ref/Microsoft.AspNetCore.Components.Web.netcoreapp.cs
                // mouse handlers
                AddEventCallback<MouseEventArgs>(builder, 5, "onmousedown", e => ActualController.HandleMouseDown(this, TranslateMouseEventArgs(e)));
                AddEventCallback<MouseEventArgs>(builder, 5, "onmousemove", e => ActualController.HandleMouseMove(this, TranslateMouseEventArgs(e)));
                AddEventCallback<MouseEventArgs>(builder, 5, "onmouseup", e => ActualController.HandleMouseUp(this, TranslateMouseEventArgs(e)));
                AddEventCallback<MouseEventArgs>(builder, 5, "onmousein", e => ActualController.HandleMouseEnter(this, TranslateMouseEventArgs(e)));
                AddEventCallback<MouseEventArgs>(builder, 5, "onmouseout", e => ActualController.HandleMouseEnter(this, TranslateMouseEventArgs(e)));
                // wheel, can't prevent default
                builder.AddAttribute(5, "onmousewheel", EventCallback.Factory.Create<WheelEventArgs>(this, e => ActualController.HandleMouseWheel(this, TranslateWheelEventArgs(e))));
                // todo: keyboard handlers --> they don't seem to work
                AddEventCallback<KeyboardEventArgs>(builder, 5, "onkeypress", e => ActualController.HandleKeyDown(this, TranslateKeyEventArgs(e)));
                // todo: add missing gesture support
            }
            builder.AddElementReferenceCapture(8, elementReference => _svg = elementReference);
            if (_svgPos.Width > 0)
            {
                var model = ((IPlotModel)this.currentModel);
                var renderer = new BlazorSvgFragmentRenderContext(builder)
                {
                    TextMeasurer = new PdfRenderContext(_svgPos.Width, _svgPos.Height, model?.Background ?? OxyColors.Transparent),
                };

                if (model != null)
                {
                    model.Update(updateDataFlag);
                    updateDataFlag = false;

                    renderer.SequenceNumber = 11;
                    if (model.Background != OxyColors.Transparent)
                    {
                        renderer.FillRectangle(OxyRect.Create(0, 0, _svgPos.Width, _svgPos.Height), model.Background);
                    }
                    renderer.SequenceNumber = 10;
                    model.Render(renderer, _svgPos.Width, _svgPos.Height);
                }
                // zoom rectangle
                if (this.zoomRectangle.Width > 0 || this.zoomRectangle.Height > 0)
                {
                    renderer.SequenceNumber = 15;
                    renderer.DrawRectangle(zoomRectangle, OxyColor.FromArgb(0x40, 0xFF, 0xFF, 0x00), OxyColors.Black, 0.5);
                }
            }
            builder.CloseElement();
            // tracker
            if (_tracker != null && _trackerEnabled)
            {
                builder.OpenElement(20, "div");
                builder.AddAttribute(21, "class", "OxyPlotTracker");
                /*                
                                builder.AddAttribute(25, "style",
                                    FormattableString.Invariant(@$"
                position: absolute;
                left: {_svgPos.Left + _tracker.Position.X:F0}px;
                top: {_svgPos.Top + _tracker.Position.Y:F0}px;
                "));
                */
                builder.AddContent(30, _tracker.Text);
                builder.CloseElement();
            }
        }

        void IDisposable.Dispose()
        {
            if (_self != null)
            {
                JSRuntime.InvokeVoidAsync("OxyPlotBlazor.removeResizeObserver", _svg);
                _self.Dispose();
                _self = null;
            }

        }

        protected async override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _self = DotNetObjectReference.Create(this);
                UpdateSvgBoundingRect(await _svg.InstallSizeChangedListener(JSRuntime, _self, nameof(UpdateSvgBoundingRect)));
            }
        }
        private static OxyModifierKeys TranslateModifierKeys(MouseEventArgs e)
        {
            var result = OxyModifierKeys.None;
            if (e.ShiftKey)
                result |= OxyModifierKeys.Shift;
            if (e.AltKey)
                result |= OxyModifierKeys.Alt;
            if (e.CtrlKey)
                result |= OxyModifierKeys.Control;
            if (e.MetaKey)
                result |= OxyModifierKeys.Windows;
            return result;
        }
        private static OxyModifierKeys TranslateModifierKeys(KeyboardEventArgs e)
        {
            var result = OxyModifierKeys.None;
            if (e.ShiftKey)
                result |= OxyModifierKeys.Shift;
            if (e.AltKey)
                result |= OxyModifierKeys.Alt;
            if (e.CtrlKey)
                result |= OxyModifierKeys.Control;
            if (e.MetaKey)
                result |= OxyModifierKeys.Windows;
            return result;
        }

        private static OxyMouseButton TranslateButton(MouseEventArgs e)
            => e.Button switch
            {
                0 => OxyMouseButton.Left,
                1 => OxyMouseButton.Middle,
                2 => OxyMouseButton.Right,
                _ => OxyMouseButton.None,
            };

        private OxyMouseDownEventArgs TranslateMouseEventArgs(MouseEventArgs e)
            => new OxyMouseDownEventArgs
            {
                Position = new ScreenPoint(e.ClientX - _svgPos.Left, e.ClientY - _svgPos.Top),
                ChangedButton = TranslateButton(e),
                ClickCount = (int)e.Detail,
                ModifierKeys = TranslateModifierKeys(e),
            };
        private OxyMouseWheelEventArgs TranslateWheelEventArgs(WheelEventArgs e)
            => new OxyMouseWheelEventArgs
            {
                Position = new ScreenPoint(e.ClientX - _svgPos.Left, e.ClientY - _svgPos.Top),
                Delta = (int)(e.DeltaY != 0 ? e.DeltaY : e.DeltaX),
                ModifierKeys = TranslateModifierKeys(e),
            };

        private static OxyKeyEventArgs TranslateKeyEventArgs(KeyboardEventArgs e)
            => new OxyKeyEventArgs
            {
                Key = Enum.TryParse<OxyKey>(e.Key, true, out var oxyKey) ? oxyKey : OxyKey.Unknown,
                ModifierKeys = TranslateModifierKeys(e),
            };

        /// <summary>
        /// translate OxyPlot Cursor type to browser cursor type name
        /// </summary>
        /// <see cref="https://developer.mozilla.org/de/docs/Web/CSS/cursor"/>
        /// <param name="cursorType"></param>
        /// <returns>browser css class cursor type</returns>
        private static string TranslateCursorType(CursorType cursorType) =>
            cursorType switch
            {
                CursorType.Pan => "grabbing",
                CursorType.ZoomRectangle => "zoom-in",
                CursorType.ZoomHorizontal => "col-resize",
                CursorType.ZoomVertical => "row-resize",
                CursorType.Default => "default",
                _ => "default",
            };

    }
}
