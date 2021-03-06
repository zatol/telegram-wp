﻿// 
// This is the source code of Telegram for Windows Phone v. 3.x.x.
// It is licensed under GNU GPL v. 2 or later.
// You should have received a copy of the license in this archive (see LICENSE).
// 
// Copyright Evgeny Nadymov, 2013-present.
// 
#define NO_RIBBON
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Caliburn.Micro;
using Coding4Fun.Toolkit.Controls;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.TL;
using TelegramClient.Behaviors;
using TelegramClient.Helpers;
using TelegramClient.Helpers.TemplateSelectors;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Media;
using TelegramClient.Views.Dialogs;
using Binding = System.Windows.Data.Binding;
using GestureEventArgs = TelegramClient.Controls.GestureListener.GestureEventArgs;

namespace TelegramClient.Views.Media
{
    public partial class ImageViewerView
    {
        public static readonly DependencyProperty TTLMediaExpiredProperty = DependencyProperty.Register(
            "TTLMediaExpired", typeof(bool), typeof(ImageViewerView), new PropertyMetadata(default(bool), OnTTLMediaExpiredChanged));

        private static void OnTTLMediaExpiredChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var imageViewerView = d as ImageViewerView;
            if (imageViewerView != null)
            {
                if ((bool) e.NewValue)
                {
                    var viewModel = imageViewerView.DataContext as ImageViewerViewModel;
                    if (viewModel != null)
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            imageViewerView._direction = CloseDirection.Down;
                            viewModel.CloseViewer();
                        });
                    }
                }
            }
        }

        public bool TTLMediaExpired
        {
            get { return (bool) GetValue(TTLMediaExpiredProperty); }
            set { SetValue(TTLMediaExpiredProperty, value); }
        }

        private readonly ApplicationBarMenuItem _savePhotoMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Save
        };

        private readonly ApplicationBarMenuItem _forwardMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Forward
        };

        private readonly ApplicationBarMenuItem _deleteMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Delete,
            IsEnabled = true
        };

        private readonly ApplicationBarMenuItem _sharePhotoMenuItem = new ApplicationBarMenuItem
        {
            Text = string.Format("{0}...", AppResources.Share)
        };

        private readonly ApplicationBarMenuItem _openMediaMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Media
        };

        public ImageViewerViewModel ViewModel
        {
            get { return DataContext as ImageViewerViewModel; }
        }

        private bool _runOnce;

        private PanAndZoomBehavior _panAndZoomBehavior;

        public ImageViewerView()
        {
            InitializeComponent();

            OptimizeFullHD();

            BuildLocalizedAppBar();

            var binding = new Binding("CurrentItem.TTLMediaExpired");
            SetBinding(TTLMediaExpiredProperty, binding);

            Visibility = Visibility.Collapsed;

            _runOnce = true;

            Loaded += (sender, args) =>
            {
                UpdateApplicationBar(ViewModel.CurrentItem as TLMessage);

                var behaviorCollection = Interaction.GetBehaviors(Control2);
                if (behaviorCollection.FirstOrDefault(x => x is PanAndZoomBehavior) == null)
                {
                    var panAndZoomBehavior = new PanAndZoomBehavior();
                    panAndZoomBehavior.SuppressDrag = true;
                    panAndZoomBehavior.MaxZoom = 5.0;
                    panAndZoomBehavior.CanZoom = true;
                    panAndZoomBehavior.DoubleTap += PanAndZoom_OnDoubleTap;

                    var canZoomBinding = new Binding("CanZoom");
                    canZoomBinding.Source = ViewModel;
                    BindingOperations.SetBinding(panAndZoomBehavior, PanAndZoomBehavior.CanZoomProperty, canZoomBinding);

                    behaviorCollection.Add(panAndZoomBehavior);

                    _panAndZoomBehavior = panAndZoomBehavior;
                }


                if (_runOnce && ViewModel.CurrentItem != null)
                {
                    _runOnce = false;
                    BeginOpenStoryboard();
                }

                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                //if (!ViewModel.ShowOpenMediaListButton)
                //{
                //    ApplicationBar.MenuItems.Remove(_openMediaMenuItem);
                //}

                if (ViewModel.DialogDetails == null)
                {
                    ApplicationBar.MenuItems.Remove(_deleteMenuItem);
                }
            };

            Unloaded += (sender, args) =>
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            };
        }

        private void OptimizeFullHD()
        {
#if WP8
            var appBar = ApplicationBar;
            if (appBar == null)
            {
                appBar = new ApplicationBar();
            }

            var appBarDefaultSize = appBar.DefaultSize;
            var appBarDifference = appBarDefaultSize - 72.0;

            AppBarPlaceholder.Height = appBarDefaultSize;
            AppBarPlaceholder2.Height = appBarDefaultSize;
            ApplicationPanel.Margin = new Thickness(0.0, 0.0, 0.0, -appBarDefaultSize);
            OpenApplicationPanelAnimationStartKeyFrame.Value = appBarDefaultSize;
            CloseApplicationPanelAnimationStopKeyFrame.Value = appBarDefaultSize;
            ApplicationPanelTransform.TranslateY = appBarDefaultSize;

            TopAppBarPlaceholder.Height = appBarDefaultSize;
            OpenTopApplicationPanelAnimationStartKeyFrame.Value = -appBarDefaultSize;
            CloseTopApplicationPanelAnimationStopKeyFrame.Value = -appBarDefaultSize;
            TopApplicationPanelTransform.TranslateY = -appBarDefaultSize;
#endif
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.CurrentItem))
            {
                UpdateApplicationBar(ViewModel.CurrentItem as TLMessage);
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.IsOpen))
            {
                if (ViewModel.IsOpen)
                {
                    _runOnce = false;
                    BeginOpenStoryboard();
                }
                else
                {
                    BeginCloseStoryboard();
                }
            }
        }

        private void UpdateApplicationBar(TLMessage message)
        {
            var isTTLMessage = message != null && message.HasTTL();

            _savePhotoMenuItem.IsEnabled =
#if WP8
                !isTTLMessage;
#else     
                !isTTLMessage && message != null && message.Media is TLMessageMediaPhoto;
#endif
            _sharePhotoMenuItem.IsEnabled = !isTTLMessage && message != null && message.Media is TLMessageMediaPhoto;
            _forwardMenuItem.IsEnabled = !isTTLMessage;
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null)
            {
                return;
            }

            ApplicationBar = new ApplicationBar { Opacity = 0.0 };
            ApplicationBar.BackgroundColor = Colors.Black;
            ApplicationBar.ForegroundColor = Colors.White;
            ApplicationBar.StateChanged += (o, e) =>
            {
                ApplicationBar.Opacity = e.IsMenuVisible ? 0.9999 : 0.0;
            };

            _savePhotoMenuItem.Click += (o, e) => ViewModel.Save();
            _sharePhotoMenuItem.Click += (o, e) => ViewModel.Share();
            _openMediaMenuItem.Click += (o, e) => ViewModel.OpenMediaDetails();
            _forwardMenuItem.Click += (o, e) => ViewModel.Forward();
            _deleteMenuItem.Click += (o, e) => ViewModel.Delete();

            ApplicationBar.MenuItems.Add(_savePhotoMenuItem);
#if WP8
            ApplicationBar.MenuItems.Add(_sharePhotoMenuItem);
#endif
            ApplicationBar.MenuItems.Add(_forwardMenuItem);
            ApplicationBar.MenuItems.Add(_deleteMenuItem);
            //ApplicationBar.MenuItems.Add(_openMediaMenuItem);
        }

        internal CloseDirection? _direction;

        private void BeginCloseStoryboard()
        {
            SystemTray.IsVisible = true;
            ApplicationBar.IsVisible = false;

            _direction = _direction ?? CloseDirection.Down;
            var duration = _direction != null ? TimeSpan.FromSeconds(0.15) : TimeSpan.FromSeconds(0.25);
            var easingFunction = _direction != null ? null : new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 5.0 };

            var storyboard = new Storyboard();

            var rootFrameHeight = ((PhoneApplicationFrame)Application.Current.RootVisual).ActualHeight;
            var translateYTo = ImagesGrid.ActualHeight / 2 + rootFrameHeight / 2;
            var translateImageAniamtion = new DoubleAnimationUsingKeyFrames();
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = duration, Value = _direction == CloseDirection.Down ? translateYTo : -translateYTo, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateImageAniamtion, ImagesGrid);
            Storyboard.SetTargetProperty(translateImageAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateImageAniamtion);

            var opacityImageAniamtion = new DoubleAnimationUsingKeyFrames();
            opacityImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = duration, Value = 0 });
            Storyboard.SetTarget(opacityImageAniamtion, BackgroundBorder);
            Storyboard.SetTargetProperty(opacityImageAniamtion, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityImageAniamtion);

            storyboard.Begin();
            storyboard.Completed += (o, args) =>
            {
                Visibility = Visibility.Collapsed;
                _direction = null;
            };

            CloseApplicationPanelAnimation.Begin();
            CloseTopApplicationPanelAnimation.Begin();
        }

        private void BeginOpenStoryboard()
        {
            var selector = (ImageViewerTemplateSelector) Resources["Selector"];

            Control1.Visibility = Visibility.Collapsed;
            Control1.Content = ViewModel.PreviousItem;
            Control1.ContentTemplate = (DataTemplate)selector.Convert(ViewModel.PreviousItem, null, null, null);
            Grid.SetColumn(Item1, 0);
            Control2.Visibility = Visibility.Visible;
            Control2.Content = ViewModel.CurrentItem;
            Control2.ContentTemplate = (DataTemplate)selector.Convert(ViewModel.CurrentItem, null, null, null);
            Grid.SetColumn(Item2, 1);
            Control3.Visibility = Visibility.Collapsed;
            Control3.Content = ViewModel.NextItem;
            Control3.ContentTemplate = (DataTemplate)selector.Convert(ViewModel.NextItem, null, null, null);
            Grid.SetColumn(Item3, 2);

            SetPanAndZoom();

            SystemTray.IsVisible = false;
            ApplicationBar.IsVisible = true;

            if (_panAndZoomBehavior != null)
            {
                _panAndZoomBehavior.CurrentScaleX = 1.0;
                _panAndZoomBehavior.CurrentScaleY = 1.0;
            }

            var transparentBlack = Colors.Black;
            transparentBlack.A = 0;

            Visibility = Visibility.Visible;
            ImagesGrid.Opacity = 1.0;
            ImagesGrid.RenderTransform = new CompositeTransform();
            BackgroundBorder.Opacity = 1.0;

            var duration = TimeSpan.FromSeconds(0.25);
            var easingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5.0 };

            var storyboard = new Storyboard();

            var rootFrameHeight = ((PhoneApplicationFrame)Application.Current.RootVisual).ActualHeight;
            var translateYTo = rootFrameHeight;

            ((CompositeTransform) ImagesGrid.RenderTransform).TranslateY = translateYTo;
            var translateImageAniamtion = new DoubleAnimationUsingKeyFrames();
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = translateYTo });
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = duration, Value = 0.0, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateImageAniamtion, ImagesGrid);
            Storyboard.SetTargetProperty(translateImageAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateImageAniamtion);

            storyboard.Completed += (sender, args) =>
            {
                Control1.Visibility = Visibility.Visible;
                Control2.Visibility = Visibility.Visible;
                Control3.Visibility = Visibility.Visible;

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    OpenApplicationPanelAnimation.Begin();
                    OpenTopApplicationPanelAnimation.Begin();
                });
            };
            Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
        }

        private void PanAndZoom_OnDoubleTap(object sender, GestureEventArgs e)
        {
            _tapHandled = true;
        }

        private bool _tapHandled;

        private void ImagesGrid_OnTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.15), () =>
            {
                if (_tapHandled)
                {
                    _tapHandled = false;
                    return;
                }

                if (ApplicationBar != null)
                {
                    ApplicationBar.IsVisible = ApplicationPanel.Visibility != Visibility.Visible;
                }

                var animation = ApplicationPanel.Visibility == Visibility.Visible
                    ? CloseApplicationPanelAnimation
                    : OpenApplicationPanelAnimation;

                animation.Begin();

                var topAnimation = TopApplicationPanel.Visibility == Visibility.Visible
                    ? CloseTopApplicationPanelAnimation
                    : OpenTopApplicationPanelAnimation;

                topAnimation.Begin();
            });
        }

        private void PanAndZoom_OnClose(object sender, PanAndZoomCloseEventArgs e)
        {
            _direction = e.VerticalChange > 0 ? CloseDirection.Down : CloseDirection.Up;

            ViewModel.CloseViewer();
        }

        private void VideoElement_OnTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            e.Handled = true;
            _tapHandled = true;
            ViewModel.OpenMedia();
        }

        private void AttachedStickers_OnTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            e.Handled = true;
            _tapHandled = true;

            var message = ViewModel.CurrentItem as TLMessage;
            if (message == null) return;

            TLInputStickeredMediaBase media = null;
            var mediaPhoto = message.Media as TLMessageMediaPhoto28;
            if (mediaPhoto != null)
            {
                var photo = mediaPhoto.Photo as TLPhoto56;
                if (photo != null)
                {
                    media = new TLInputStickeredMediaPhoto
                    {
                        Id = new TLInputPhoto { Id = photo.Id, AccessHash = photo.AccessHash }
                    };
                }
            }

            var mediaDocument = message.Media as TLMessageMediaDocument45;
            if (mediaDocument != null)
            {
                var document = mediaDocument.Document as TLDocument54;
                if (document != null)
                {
                    media = new TLInputStickeredMediaDocument
                    {
                        Id = new TLInputDocument { Id = document.Id, AccessHash = document.AccessHash }
                    };
                }
            }

            if (media == null) return;
            ViewModel.IsWorking = true;
            var mtProtoService = IoC.Get<IMTProtoService>();
            mtProtoService.GetAttachedStickersAsync(true, media,
                result => Execute.BeginOnUIThread(() =>
                {
                    ViewModel.AttachedStickers = result;
                    ViewModel.IsWorking = false;
                    var r = result;
                    if (r != null)
                    {
                        UpdateSets(r);

                        ShowAttachedStickersMessageBox(r, stickerSetBase =>
                        {
                            var stickerSet = stickerSetBase as TLStickerSet32;
                            if (stickerSet != null)
                            {
                                _panAndZoomBehavior.IsEnabled = false;
                                ShowStickerSetMessageBox(false, stickerSet.Installed && !stickerSet.Archived, stickerSet, prompt =>
                                {
                                    Execute.BeginOnUIThread(() =>
                                    {
                                        _panAndZoomBehavior.IsEnabled = true;
                                        if (prompt == PopUpResult.Ok)
                                        {
                                            ViewModel.AddRemoveStickerSet(stickerSet);
                                        }
                                    });
                                });
                            }
                        });
                        
                    }
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    ViewModel.IsWorking = false;
                    Telegram.Api.Helpers.Execute.ShowDebugMessage("messages.getAttachedStickers error " + error);
                }));
        }


        private readonly Dictionary<string, TLVector<TLStickerItem>> _stickerSets = new Dictionary<string, TLVector<TLStickerItem>>();

        private readonly Dictionary<long, string> _emoticons = new Dictionary<long, string>();

        private IStickers _stickers;

        private TLVector<TLMessagesStickerSet> _messagesStickerSets;

        private void UpdateSets(IStickers iStickers)
        {
            _stickers = iStickers;

            _emoticons.Clear();
            _stickerSets.Clear();

            for (var i = 0; i < iStickers.Documents.Count; i++)
            {
                var document22 = iStickers.Documents[i] as TLDocument22;
                if (document22 != null)
                {
                    if (document22.StickerSet != null)
                    {
                        var setId = document22.StickerSet.Name;
                        TLVector<TLStickerItem> stickers;
                        if (_stickerSets.TryGetValue(setId, out stickers))
                        {
                            stickers.Add(new TLStickerItem { Document = document22 });
                        }
                        else
                        {
                            _stickerSets[setId] = new TLVector<TLStickerItem> { new TLStickerItem { Document = document22 } };
                        }
                    }
                }
            }

            var firstChunkSize = 10;
            var count = 0;
            var delayedItems = new List<TLStickerSetBase>();
            for (var i = 0; i < iStickers.Sets.Count; i++)
            {
                var set = iStickers.Sets[i];

                var setName = set.Id.ToString();
                TLVector<TLStickerItem> stickers;
                if (_stickerSets.TryGetValue(setName, out stickers))
                {

                    var objects = new TLVector<TLObject>();
                    foreach (var sticker in stickers)
                    {
                        objects.Add(sticker);
                    }

                    set.Stickers = objects;
                }
            }
        }

        private bool? _verticalDrag;
        private TLMessageBase _currentItem;

        private void ImagesGrid_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            e.Handled = true;

            if (_panAndZoomBehavior.CurrentScaleX > 1.0) return;
            if (_slideAnimating) return;

            _verticalDrag = null;
        }

        private void ImagesGrid_OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            e.Handled = true;

            if (_panAndZoomBehavior.CurrentScaleX > 1.0) return;
            if (_slideAnimating) return;

            if (_verticalDrag == null)
            {
                _verticalDrag = Math.Abs(e.DeltaManipulation.Translation.Y) >
                                  Math.Abs(e.DeltaManipulation.Translation.X);
            }
            if (_verticalDrag == false)
            {
                var transform = ImagesGrid.RenderTransform as CompositeTransform;
                if (transform == null) return;

                if (e.CumulativeManipulation.Translation.X < 0.0
                    && ViewModel.CanSlideLeft)
                {
                    transform.TranslateX += e.DeltaManipulation.Translation.X;
                }
                else if (e.CumulativeManipulation.Translation.X > 0.0
                    && ViewModel.CanSlideRight)
                {
                    transform.TranslateX  += e.DeltaManipulation.Translation.X;
                }

                var percent = transform.TranslateX / 480.0;
#if !NO_RIBBON
                Ribbon.Move(percent);
#endif

                transform.TranslateX = PanAndZoomBehavior.Clamp(transform.TranslateX, -480.0 - 12.0, 480.0 + 12.0);
                _panAndZoomBehavior.IsEnabled = transform.TranslateX == 0.0;
            }
            else
            {
                var transform = ImagesGrid.RenderTransform as CompositeTransform;
                if (transform == null) return;

                var translate = new CompositeTransform
                {
                    TranslateX = 0,
                    TranslateY = e.CumulativeManipulation.Translation.Y
                };

                var rootFrameHeight = ((PhoneApplicationFrame)Application.Current.RootVisual).ActualHeight;
                var deltaY = Math.Abs(translate.TranslateY + translate.ScaleY * transform.TranslateY + (translate.ScaleY - 1) * (transform.CenterY - translate.CenterY));
                var opacity = (rootFrameHeight - deltaY) / rootFrameHeight;
                var backgroundBrush = (SolidColorBrush)BackgroundBorder.Background;
                var backgroundColor = backgroundBrush.Color;
                backgroundColor.A = (byte)(opacity * byte.MaxValue);

                BackgroundBorder.Opacity = opacity;

                ImagesGrid.RenderTransform = translate;
                _panAndZoomBehavior.IsEnabled = translate.TranslateY == 0.0;
            }
        }

        private void ImagesGrid_OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            e.Handled = true;

            if (_panAndZoomBehavior.CurrentScaleX > 1.0) return;
            if (_slideAnimating) return;

            _panAndZoomBehavior.IsEnabled = true;
            if (_verticalDrag == true)
            {
                var transform = ImagesGrid.RenderTransform as CompositeTransform;
                if (transform == null) return;

                var hasVelocity = e.FinalVelocities.LinearVelocity.X > 1000.0;
                var minTranslateY = 100.0;

                if (Math.Abs(transform.TranslateY) > minTranslateY || hasVelocity && Math.Abs(transform.TranslateY) > 0.0)
                {
                    PanAndZoom_OnClose(this, new PanAndZoomCloseEventArgs { VerticalChange = e.TotalManipulation.Translation.Y });
                    return;
                }

                var storyboard = new Storyboard();

                var translateAnimation = new DoubleAnimationUsingKeyFrames();
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 } });
                Storyboard.SetTarget(translateAnimation, ImagesGrid);
                Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                storyboard.Children.Add(translateAnimation);

                storyboard.Begin();

                BackgroundBorder.Opacity = 1.0;
            }
            else if (_verticalDrag == false)
            {
                var transform = ImagesGrid.RenderTransform as CompositeTransform;
                if (transform == null) return;

                var hasVelocity = Math.Abs(e.FinalVelocities.LinearVelocity.X) > 1000.0;
                var minTranslateX = 160.0;

                if (transform.TranslateX > minTranslateX || hasVelocity && transform.TranslateX > 0.0)
                {
                    SlideRight(Math.Abs(e.FinalVelocities.LinearVelocity.X));
                }
                else if (transform.TranslateX < -minTranslateX || hasVelocity && transform.TranslateX < 0.0)
                {
                    SlideLeft(Math.Abs(e.FinalVelocities.LinearVelocity.X));
                }
                else
                {
                    SlideBack();
                    transform.TranslateX = 0.0;
                }
            }
        }

        private bool _slideAnimating;

        public void SlideLeft(double velocity, System.Action callback = null)
        {
            if (velocity == 0.0)
            {
                velocity = 480.0 + 12.0;
            }

            var transform = ImagesGrid.RenderTransform as CompositeTransform;
            if (transform == null) return;

            var translationX = 480.0 + 12.0;
            var duration = PanAndZoomBehavior.Clamp((translationX + transform.TranslateX) / velocity, .15, .35);
#if !NO_RIBBON
            Ribbon.ScrollNext(duration);
#endif

            var storyboard = new Storyboard();
            var translateAnimation = new DoubleAnimation();
            translateAnimation.To = -translationX;
            translateAnimation.Duration = new Duration(TimeSpan.FromSeconds(duration));
            Storyboard.SetTarget(translateAnimation, transform);
            Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("TranslateX"));
            storyboard.Children.Add(translateAnimation);
            storyboard.Begin();

            //MainGrid.IsHitTestVisible = false;
            _slideAnimating = true;
            storyboard.Completed += (sender, args) =>
            {
                //Deployment.Current.Dispatcher.BeginInvoke(() =>
                //{
                    //MainGrid.IsHitTestVisible = true;
                    _slideAnimating = false;
                //});
                transform.TranslateX = 0.0;
                Grid.SetColumn(Item1, Grid.GetColumn(Item1) - 1 < 0 ? 2 : Grid.GetColumn(Item1) - 1);
                Grid.SetColumn(Item2, Grid.GetColumn(Item2) - 1 < 0 ? 2 : Grid.GetColumn(Item2) - 1);
                Grid.SetColumn(Item3, Grid.GetColumn(Item3) - 1 < 0 ? 2 : Grid.GetColumn(Item3) - 1);

                SetPanAndZoom();
                //return;
                ViewModel.SlideLeft();
                //return;
                SetControlContent(2, ViewModel.NextItem);

                callback.SafeInvoke();
            };
        }

        public void SlideRight(double velocity, System.Action callback = null)
        {
            if (velocity == 0.0)
            {
                velocity = 480.0 + 12.0;
            }

            var transform = ImagesGrid.RenderTransform as CompositeTransform;
            if (transform == null) return;

            var translationX = 480.0 + 12.0;
            var duration = PanAndZoomBehavior.Clamp((translationX - transform.TranslateX) / velocity, .15, .35);

#if !NO_RIBBON
            Ribbon.ScrollPrevious(duration);
#endif

            var storyboard = new Storyboard();
            var translateAnimation = new DoubleAnimation();
            translateAnimation.To = translationX;
            translateAnimation.Duration = new Duration(TimeSpan.FromSeconds(duration));
            Storyboard.SetTarget(translateAnimation, transform);
            Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("TranslateX"));
            storyboard.Children.Add(translateAnimation);
            storyboard.Begin();
            //MainGrid.IsHitTestVisible = false;
            _slideAnimating = true;
            storyboard.Completed += (sender, args) =>
            {
                //Deployment.Current.Dispatcher.BeginInvoke(() =>
               // {
                    //MainGrid.IsHitTestVisible = true;
                    _slideAnimating = false;
                //});
                //MainGrid.IsHitTestVisible = true;
                transform.TranslateX = 0.0;

                Grid.SetColumn(Item1, Grid.GetColumn(Item1) + 1 > 2 ? 0 : Grid.GetColumn(Item1) + 1);
                Grid.SetColumn(Item2, Grid.GetColumn(Item2) + 1 > 2 ? 0 : Grid.GetColumn(Item2) + 1);
                Grid.SetColumn(Item3, Grid.GetColumn(Item3) + 1 > 2 ? 0 : Grid.GetColumn(Item3) + 1);

                SetPanAndZoom();
                //return;
                ViewModel.SlideRight();
                //return;
                SetControlContent(0, ViewModel.PreviousItem);

                callback.SafeInvoke();
            };
        }

        public void SetControlContent(int column, TLMessageBase content)
        {
            var selector = (ImageViewerTemplateSelector) Resources["Selector"];

            if (Grid.GetColumn(Item1) == column)
            {
                Control1.Content = content;
                Control1.ContentTemplate = (DataTemplate) selector.Convert(content, null, null, null);
            }
            else if (Grid.GetColumn(Item2) == column)
            {
                Control2.Content = content;
                Control2.ContentTemplate = (DataTemplate) selector.Convert(content, null, null, null);
            }
            else if (Grid.GetColumn(Item3) == column)
            {
                Control3.Content = content;
                Control3.ContentTemplate = (DataTemplate) selector.Convert(content, null, null, null);
            }
        }

        private void SlideBack()
        {
            var transform = ImagesGrid.RenderTransform as CompositeTransform;
            if (transform == null) return;

            var duration = 0.1;

            var storyboard = new Storyboard();
            var translateAnimation = new DoubleAnimation();
            translateAnimation.To = 0.0;
            translateAnimation.Duration = new Duration(TimeSpan.FromSeconds(duration));            
            Storyboard.SetTarget(translateAnimation, transform);
            Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("TranslateX"));
            storyboard.Children.Add(translateAnimation);
            storyboard.Begin();

#if !NO_RIBBON
            Ribbon.ScrollBack(duration);
#endif
        }

        private void SetPanAndZoom()
        {
            Interaction.GetBehaviors(Control1).Clear();
            Interaction.GetBehaviors(Control2).Clear();
            Interaction.GetBehaviors(Control3).Clear();
            //Control1.CacheMode = null;
            //Control2.CacheMode = null;
            //Control3.CacheMode = null;

            if (Grid.GetColumn(Item1) == 1)
            {
                var behaviors = Interaction.GetBehaviors(Control1);
                behaviors.Add(_panAndZoomBehavior);
                Control1.RenderTransform = new CompositeTransform();
                //Control1.CacheMode = new BitmapCache();
            }
            else if (Grid.GetColumn(Item2) == 1)
            {
                var behaviors = Interaction.GetBehaviors(Control2);
                behaviors.Add(_panAndZoomBehavior);
                Control2.RenderTransform = new CompositeTransform();
                //Control2.CacheMode = new BitmapCache();
            }
            else if (Grid.GetColumn(Item3) == 1)
            {
                var behaviors = Interaction.GetBehaviors(Control3);
                behaviors.Add(_panAndZoomBehavior);
                Control3.RenderTransform = new CompositeTransform();
                //Control3.CacheMode = new BitmapCache();
            }
        }

        private int _index;
        private double _duration;

        public void ScrollTo(TLMessageBase currentItem, double duration)
        {
#if !NO_RIBBON
            var index = ViewModel.GroupedItems.IndexOf(currentItem);
            if (index != -1)
            {
                _index = index;
                _duration = duration;

                Ribbon.LayoutUpdated += Ribbon_LayoutUpdated;
            }
#endif
        }

        private void Ribbon_LayoutUpdated(object sender, System.EventArgs e)
        {
#if !NO_RIBBON
            var result = Ribbon.ScrollTo(_index, _duration);
            System.Diagnostics.Debug.WriteLine("Ribbon.ScrollTo result=" + result);
            if (result)
            {
                Ribbon.LayoutUpdated -= Ribbon_LayoutUpdated;
            }
#endif
        }
    }

    public enum CloseDirection
    {
        Down,
        Up
    }
}