Hello, and thank you for taking the time to interview with GitHub!
 
In this interview, you will design and implement a small load balancer library. You are encouraged to use an AI assistant during the exercise. The goal is to understand how you use AI as part of a real engineering workflow, not just whether the final code works.
 
General Guidelines:
 
- You may choose any programming language.
- You may use an AI assistant for research, planning, implementation, tests, debugging, and refactoring.
- You should explain how you are using AI and what you are accepting or changing from its output.
- You are encouraged to ask clarifying questions and share your thinking as you go.
- You may not use code that you wrote before this exercise.
- You may use third-party libraries if available, but explain why they are needed.
- Prefer a small, working solution over an elaborate design that cannot be validated.
 
## Problem
 
Write a load balancer library that helps a caller choose which backend service instance should handle each request.
 
At minimum, your library should:
 
1. Be initialized with a list of backend nodes.
2. Return the next backend that should receive a request.
3. Distribute requests predictably and fairly across the configured backends.
4. Handle edge cases such as an empty backend list.
5. Include tests, examples, or a small sample program that demonstrates the expected behavior.
 
You may decide the public API, data structures, and initial balancing strategy. Be prepared to explain your choices and the trade-offs.
 
## Example behavior
 
If the configured backends are `A`, `B`, and `C`, repeated calls might return:
 
```text
A, B, C, A, B, C
```
 
The exact API is up to you. For example, you might expose a method like:
 
```text
nextBackend()
```
 
or:
 
```text
selectBackend(request)
```
 
Use your time wisely. Start with a clear plan, get something working, and then iterate based on feedback.
- not a high throughtput
- healthy check for nodes
- 