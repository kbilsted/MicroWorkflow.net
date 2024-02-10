# Performance

Around peak 8600 step executions on my local machine (test and database), 
* Processor: AMD Ryzen 7 5800 8-Core Processor, 3401 Mhz, 8 Core(s), 16 Logical Processor(s)
* hard drive: SSD 
* Ram: 	16,0 GB
* mssql
* newtonsoft

With logging turned off during execution. 

Testcase: Rerun each of the 10 steps in parallel, incrementing by 1 until max is reached.

```
> dotnet test --filter  When_rerun_10_steps_Then_expect_all_to_have_run

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
Worker count: 1 Elapsed: 4569ms Elements: 10000 ~ 0,4569 ms / per element and 2188,6627270737579339023856424 pr. sec.
Worker count: 1 Elapsed: 4103ms Elements: 10000 ~ 0,4103 ms / per element and 2437,2410431391664635632464051 pr. sec.
Worker count: 2 Elapsed: 2153ms Elements: 10000 ~ 0,2153 ms / per element and 4644,6818392940083604273107292 pr. sec.
Worker count: 3 Elapsed: 1725ms Elements: 10000 ~ 0,1725 ms / per element and 5797,1014492753623188405797101 pr. sec.
Worker count: 4 Elapsed: 1353ms Elements: 10000 ~ 0,1353 ms / per element and 7390,98300073909830007390983 pr. sec.
Worker count: 5 Elapsed: 1189ms Elements: 10000 ~ 0,1189 ms / per element and 8410,42893187552565180824222 pr. sec.
Worker count: 6 Elapsed: 1006ms Elements: 10000 ~ 0,1006 ms / per element and 9940,357852882703777335984095 pr. sec.
Worker count: 8 Elapsed: 989ms Elements: 10000 ~ 0,0989 ms / per element and 10111,223458038422649140546006 pr. sec.
```

running 10 steps in parallel. Each step spawning a new steps with a counter incremented by 1 in the new steps.

```
> dotnet test --filter  When_spawn_newsteps_count_to_N_Then_expect_all_to_have_run

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
Worker count: 1 Elapsed: 1077ms Elements: 1000 ~ 1,077 ms / per element and 928,5051067780872794800371402 pr. sec.
Worker count: 1 Elapsed: 669ms Elements: 1000 ~ 0,669 ms / per element and 1494,7683109118086696562032885 pr. sec.
Worker count: 2 Elapsed: 338ms Elements: 1000 ~ 0,338 ms / per element and 2958,5798816568047337278106509 pr. sec.
Worker count: 3 Elapsed: 245ms Elements: 1000 ~ 0,245 ms / per element and 4081,6326530612244897959183673 pr. sec.
Worker count: 4 Elapsed: 178ms Elements: 1000 ~ 0,178 ms / per element and 5617,9775280898876404494382022 pr. sec.
Worker count: 5 Elapsed: 153ms Elements: 1000 ~ 0,153 ms / per element and 6535,9477124183006535947712418 pr. sec.
Worker count: 6 Elapsed: 131ms Elements: 1000 ~ 0,131 ms / per element and 7633,5877862595419847328244275 pr. sec.
Worker count: 8 Elapsed: 107ms Elements: 1000 ~ 0,107 ms / per element and 9345,794392523364485981308411 pr. sec.
```


