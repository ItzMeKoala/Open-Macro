// Several tests use real threads, timers, local web servers and sockets; run side by side on a two-core build machine they
// starve each other of threads and fail on timeouts. The whole suite takes only a few seconds, so it runs one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
