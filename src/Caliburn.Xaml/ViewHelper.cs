﻿using System;
using System.Collections.Generic;
using System.Reflection;
#if NETFX_CORE
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
#else
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
#endif

namespace Caliburn.Light
{
    /// <summary>
    /// Some helper methods when dealing with UI elements.
    /// </summary>
    public static class ViewHelper
    {
        private static bool? _isInDesignTool;

        /// <summary>
        /// Gets a value that indicates whether the process is running in design mode.
        /// </summary>
        public static bool IsInDesignTool
        {
            get
            {
                if (!_isInDesignTool.HasValue)
                {
#if NETFX_CORE
                    _isInDesignTool = DesignMode.DesignModeEnabled;
#elif SILVERLIGHT
	                _isInDesignTool = DesignerProperties.IsInDesignTool;
#else
                    var descriptor = DependencyPropertyDescriptor.FromProperty(DesignerProperties.IsInDesignModeProperty,
                        typeof (FrameworkElement));
                    _isInDesignTool = (bool)descriptor.Metadata.DefaultValue;
#endif
                }

                return _isInDesignTool.Value;
            }
        }

        private static readonly DependencyProperty IsGeneratedProperty =
            DependencyProperty.RegisterAttached("IsGenerated", typeof (bool), typeof (ViewHelper), null);

        /// <summary>
        /// Used to retrieve the root, non-framework-created view.
        /// </summary>
        /// <param name="view">The view to search.</param>
        /// <returns>The root element that was not created by the framework.</returns>
        /// <remarks>In certain instances the services create UI elements.
        /// For example, if you ask the window manager to show a UserControl as a dialog, it creates a window to host the UserControl in.
        /// The WindowManager marks that element as a framework-created element so that it can determine what it created vs. what was intended by the developer.
        /// Calling GetFirstNonGeneratedView allows the framework to discover what the original element was. 
        /// </remarks>
        public static object GetFirstNonGeneratedView(DependencyObject view)
        {
            if (!(bool) view.GetValue(IsGeneratedProperty)) return view;

            var contentControl = view as ContentControl;
            if (contentControl != null)
                return contentControl.Content;

            var contentProperty = FindContentProperty(view);
            return contentProperty.GetValue(view);
        }

        /// <summary>
        /// Sets the IsGenerated property for <paramref name="view"/>.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="value">true, if the view is generated by the framework.</param>
        public static void SetIsGenerated(DependencyObject view, bool value)
        {
            view.SetValue(IsGeneratedProperty, value);
        }

        private static readonly DependencyProperty PreviouslyAttachedProperty =
            DependencyProperty.RegisterAttached("PreviouslyAttached", typeof (bool), typeof (ViewHelper), null);

        /// <summary>
        /// Executes the handler the fist time the element is loaded.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="handler">The handler.</param>
        public static void ExecuteOnFirstLoad(FrameworkElement element, Action<FrameworkElement> handler)
        {
            if ((bool) element.GetValue(PreviouslyAttachedProperty)) return;
            element.SetValue(PreviouslyAttachedProperty, true);
            ExecuteOnLoad(element, handler);
        }

        /// <summary>
        /// Executes the handler immediately if the element is loaded, otherwise wires it to the Loaded event.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="handler">The handler.</param>
        /// <returns>true if the handler was executed immediately; false otherwise</returns>
        public static bool ExecuteOnLoad(FrameworkElement element, Action<FrameworkElement> handler)
        {
            if (IsElementLoaded(element))
            {
                handler(element);
                return true;
            }

            RoutedEventHandler loaded = null;
            loaded = delegate
            {
                element.Loaded -= loaded;
                handler(element);
            };
            element.Loaded += loaded;
            return false;
        }

        /// <summary>
        /// Executes the handler when the element is unloaded.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="handler">The handler.</param>
        public static void ExecuteOnUnload(FrameworkElement element, Action<FrameworkElement> handler)
        {
            RoutedEventHandler unloaded = null;
            unloaded = delegate
            {
                element.Unloaded -= unloaded;
                handler(element);
            };
            element.Unloaded += unloaded;
        }

        /// <summary>
        /// Executes the handler the next time the elements's LayoutUpdated event fires.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="handler">The handler.</param>
        public static void ExecuteOnLayoutUpdated(FrameworkElement element, Action<FrameworkElement> handler)
        {
#if NETFX_CORE
            EventHandler<object> onLayoutUpdate = null;
#else
            EventHandler onLayoutUpdate = null;
#endif
            onLayoutUpdate = delegate
            {
                element.LayoutUpdated -= onLayoutUpdate;
                handler(element);
            };
            element.LayoutUpdated += onLayoutUpdate;
        }

        /// <summary>
        /// Determines whether the specified <paramref name="element"/> is loaded.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>true if the element is loaded; otherwise, false.
        /// </returns>
        public static bool IsElementLoaded(FrameworkElement element)
        {
            if (element == null)
                return false;

#if NETFX_CORE
            var content = Window.Current.Content;
            var parent = element.Parent ?? VisualTreeHelper.GetParent(element);
            return parent != null || (content != null && element == content);
#elif SILVERLIGHT
            //var root = Application.Current.RootVisual;
            var parent = element.Parent ?? VisualTreeHelper.GetParent(element);
            var children = VisualTreeHelper.GetChildrenCount(element);
            return parent != null && children > 0;
#else
            return element.IsLoaded;
#endif
        }

        private const string DefaultContentPropertyName = "Content";

        /// <summary>
        /// Finds the Content property of specified <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>An object that represents the specified property, or null if the property is not found.</returns>
        public static PropertyInfo FindContentProperty(object element)
        {
            var type = element.GetType();
            var contentPropertyAttribute = type.GetTypeInfo().GetCustomAttribute<ContentPropertyAttribute>(true);
            var contentPropertyName = (contentPropertyAttribute == null) ? DefaultContentPropertyName : contentPropertyAttribute.Name;
            return type.GetRuntimeProperty(contentPropertyName);
        }

        /// <summary>
        /// Applies the <paramref name="settings"/> to the <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="settings">A list of property-name/value pairs.</param>
        public static void ApplySettings(object target, IEnumerable<KeyValuePair<string, object>> settings)
        {
            if (settings == null) return;

            var type = target.GetType();
            foreach (var pair in settings)
            {
                var propertyInfo = type.GetRuntimeProperty(pair.Key);
                if (propertyInfo != null)
                    propertyInfo.SetValue(target, pair.Value, null);
            }
        }
    }
}
