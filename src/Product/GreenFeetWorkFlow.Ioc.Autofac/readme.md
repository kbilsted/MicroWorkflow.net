# Autofac integration

This package provides helper methods to easily integrate with and to scan assemblies for step implementations.

## Getting started

To use Autofac as the IOC container you need to register it 

```
builder.RegisterType<AutofacAdaptor>().As<IWorkflowIocContainer>();
```

If you want to scan assemblies for workflow step implementations using the `[StepName]` attribute, you can use `builder.RegisterStepImplementations(GetType().Assembly)`. Otherwise you can manually register step implementations with
 `builder.RegisterType(implementationType).Named<IStepImplementation>(stepName);`

A complete example of a usage is found in https://github.com/kbilsted/GreenFeetWorkFlow/blob/master/src/Demos/GreenFeetWorkFlow.WebApiDemo/RegisterGreenFeetWF.cs
