= About SIPCRaft

This is the start of a new enterprise SIP server in C# using the SIPSorcery
stack. The server will operate optionally in conjunction with a postgresql data
server and likely a golang management system. To maintain high availability
sipcraft will cache data tables such as extensions locally and can operate
with intermittent database connection loss with support for automatic
backend resynchronization. If no backend database is used the config file
alone will supply extension information, which can also be used for stand-alone
development testing.

## Dependencies

SIPCraft is a C# application that uses Net 8.0 or later framework and the
SIPSorcery sip stack and the Tychosoft Extensions package (tychoext) for C#. It
also will make use of async programming for sip services. SIPCraft can be built
and run either directly on Microsoft Windows or on Posix platforms with
dotnet support such as Alpine Linux. SIPCraft should also be able to run in a
docker instance.

## Posix platforms

A Makefile is used to standardize build behavior and development practices on
Posix systems and for building Docker images. On Microsoft Windows one can of
course use Visual Studio. On Posix systems I use AOT compiling to create a
single standard posix executable with native debugging support.

The posix Makefile does include an ``install'' target. This can be used to
produce a traditional installation package such as Debian packages for Ubuntu
GNU/Linux systems. Similarly, the ``dist'' target can be used to create a
stand-alone source tarball for use with packaging systems.

## Participation

Once it becomes useful this project will have a public project page at
https://www.github.com/tychosoft/sipcraft which will have an issue tracker
where people can submit public bug reports, a wiki for hosting project
documentation and architecture, and a public git repository. Patches and merge
requests may be submitted in the issue tracker or thru email then. For now it
is in very early development.

