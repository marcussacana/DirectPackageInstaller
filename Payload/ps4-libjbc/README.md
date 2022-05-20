# libjbc

This a firmware-agnostic implementation of the sandbox escape for PS4 homebrew apps. It operates by traversing the process list up to PID 1 (init) and copying its prison and rdir into the calling process.

In the future more functionality (e.g.Mira-style "mount in sandbox") may be supported.
