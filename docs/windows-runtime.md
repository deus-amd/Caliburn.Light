# Windows Runtime specifics

Unlike the WPF version of Caliburn.Light the WinRT version doesn't use a Bootstrapper, the non ranty reason for this is that Windows.UI.Xaml.Application exposes most of it's functionality through method overrides and not events. Therefore it makes sense to have a custom Application rather than forcing the developer to wire the application to the bootstrapper.

Rather than creating a new Bootstrapper in WinRT we replace the existing Application with one of our own. In App.xaml replace the Application instance with CaliburnApplication.

``` xml
<caliburn:CaliburnApplication
    x:Class="Caliburn.Light.WinRT.Sample.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:caliburn="using:Caliburn.Light">

</caliburn:CaliburnApplication>
```

The functionality of this new CaliburnApplication is very similar to the previous Bootstrapper. Do expect some breaking changes regarding the functionality of handling the default view, launch arguments and types.

In App.xaml.cs you would typically start with the following code:

``` csharp
using System;
using System.Collections.Generic;
using Caliburn.Light.WinRT.Sample.Views;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml.Controls;

namespace Caliburn.Light.WinRT.Sample
{
    public sealed partial class App
    {
        public App()
        {
            InitializeComponent();
        }

        private SimpleContainer _container;

        protected override void Configure()
        {
            _container = new SimpleContainer();
            IoC.Initialize(_container);

            _container.RegisterSingleton<INavigationService, FrameAdapter>();
            _container.RegisterSingleton<IEventAggregator, EventAggregator>();
            _container.RegisterSingleton<IViewModelLocator, ViewModelLocator>();
            _container.RegisterSingleton<IViewModelBinder, ViewModelBinder>();

            var typeResolver = new NameBasedViewModelTypeResolver();
            typeResolver.AddAssembly(typeof(App).GetTypeInfo().Assembly);
            _container.RegisterInstance<IViewModelTypeResolver>(typeResolver);

            //TODO: Register your view models at the container
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
 			if (args.PreviousExecutionState == ApplicationExecutionState.Running)
                return;

            DisplayRootView<MenuView>();
        }
    }
}
```

Caliburn.Light on its various platforms has usually supported either a View Model first or a View first approach, but not usually both at the same time. Typically WPF applications follow a View Model first approach, usually with a Shell View Model and using view model composition. Meanwhile Windows Phone applications due to having the navigation concept baked very close to the hardware (with the back button) typically follow a View first approach and expose a navigation service to move between pages.

Windows Store apps sit somewhere in the middle, since there is no hardware back button we don’t have the same drive towards View first, however most apps follow a design where a root Frame control navigating between pages makes sense. However there is nothing stopping a developer using a View Model first approach and for certain apps and scenarios this makes sense. In fact I feel that fully featured apps will use both approaches.

Here’s some examples of how you’d run through each scenario.

### View First

This approach is what you’re used to if you’ve been using the WinRT version up until now, it’s fundamentally the same with a few minor changes in your App.xaml.cs. We override a method named PrepareViewFirst that has a parameter of the root frame for the application (this is also accessible through the RootFrame property). We can then pass this to the container, this creates the required FrameAdapter and registers it to the container as a navigation service. If you’re using a different container this is where you’d do the same with your own container.

``` csharp
protected override void PrepareViewFirst(Frame rootFrame)
{
  _container.RegisterInstance(rootFrame);
  _container.GetInstance<INavigationService>();
}
```

Now instead of defining the default view we’ll override OnLaunched, this is the method called by Windows 8 on launch. Here we’ll call DisplayRootView with the type of the view we want our root frame to navigate to, in this case MenuView. This approach enables us to use things like the launch arguments and choose a different view to navigate to. Then Caliburn.Light will ensure the navigation system is initialized, set the root frame as the content of the window and navigate to the specified view.

``` csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
  DisplayRootView<MenuView>();
}
```

### View Model First

This approach takes advantage of the view model composition built into Caliburn.Light. It’s a little simpler to set up than view first. We don’t need to override PrepareViewFirst and in our OnLaunched we call DisplayRootViewFor with the view model as the type. Caliburn determines the view as per its conventions and sets that as the content of the window as well as binding the two together.

``` csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
  DisplayRootViewFor<ShellViewModel>();
}
```

##### Things to watch out for

Due to the event model of a WinRT application there isn’t a suitable place to initialize the CaliburnApplication before OnLaunched, therefore both DisplayRootView and DisplayRootViewFor will Initialize the application (and ultimately call Configure). If you’re dependent upon the application already being configured then you can call Initialize yourself.

### Combining both approaches

Any significantly sized WinRT application will most likely use a combination of the two approaches. While you may use View first to quickly enable the navigation metaphor with a back button you may compose individual views with child view models. Also some launch scenarios such as Share Target give you a small window to work in where View Model first will be simpler. The sample WinRT application shows this approach.

Overall these changes should give you increased functionality and better control of your application using Caliburn.Light.


### Dealing with Fast App Resume

On Windows 8 OnLaunched will only be called when your app is launched, if your app is already running and the user relaunches then the app will be activated but not launched and opened where it currently is.

On Windows Phone 8.1 then OnLaunched will be called with a `PreviousExecutionState` of `ApplicationExecutionState.Running`. You need to check for this to not overwrite the current state of the app.

## Attaching an action

In contrast to WPF
- does not use behaviors (Behaviors SDK)
- use `x:Bind` for sake of performance
- use `cal:Bind.CommandParameter` in combination with `x:Bind`
- maintain the Enabled state with a separate binding
