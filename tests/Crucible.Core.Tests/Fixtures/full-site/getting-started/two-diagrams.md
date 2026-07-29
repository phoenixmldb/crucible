---
title: Two Diagrams
sort: 3
---

# Two Diagrams

A page carrying more than one diagram, so the theme's mermaid runtime is
included once per page rather than once per diagram.

```mermaid
graph LR; A-->B; B-->C;
```

Some prose between the two.

```mermaid
sequenceDiagram
    Alice->>Bob: Hello
    Bob-->>Alice: Hi
```
