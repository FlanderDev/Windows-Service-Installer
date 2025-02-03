# WindowsServiceInstaller
Handles creating/deleting services.

It takes up to two optional arguments (in order).
- The filePath of the .exe, which shall be started by the service.
- What should happen to the service.
    - "install": installs and starts the service.
    - "uninstall": stops and uninstalls the service.
