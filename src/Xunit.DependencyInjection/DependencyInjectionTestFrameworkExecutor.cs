﻿namespace Xunit.DependencyInjection;

public class DependencyInjectionTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    private readonly HostManager _hostManager;

    public DependencyInjectionTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink) =>
        DisposalTracker.Add(_hostManager = new(assemblyName, diagnosticMessageSink));

    /// <inheritdoc />
    protected override async void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        var exceptions = new List<Exception>();
        var host = GetHost(exceptions, _hostManager.BuildDefaultHost);

        static IHost? GetHost(ICollection<Exception> exceptions, Func<IHost?> func)
        {
            try
            {
                return func();
            }
            catch (TargetInvocationException tie)
            {
                exceptions.Add(tie.InnerException);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            return null;
        }

        // ReSharper disable once PossibleMultipleEnumeration
        var hostMap = testCases
            .GroupBy(tc => tc.TestMethod.TestClass)
            .ToDictionary(group => group.Key, group => GetHost(exceptions, () => _hostManager.GetHost(group.Key.Class.ToRuntimeType())));

        try
        {
            await _hostManager.StartAsync(default).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }

        using var runner = new DependencyInjectionTestAssemblyRunner(host?.Services, TestAssembly,
            // ReSharper disable once PossibleMultipleEnumeration
            testCases, hostMap, DiagnosticMessageSink, executionMessageSink, executionOptions, exceptions);

        await runner.RunAsync().ConfigureAwait(false);

        await _hostManager.StopAsync(default).ConfigureAwait(false);
    }
}
