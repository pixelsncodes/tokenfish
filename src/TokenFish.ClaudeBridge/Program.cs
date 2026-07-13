using TokenFish.Providers.Claude;

var runner = new ClaudeBridgeConsoleRunner(new ClaudeBridgeStateFileStore());
return await runner.RunAsync(
    args,
    Console.OpenStandardInput(),
    Console.Out,
    CancellationToken.None);
