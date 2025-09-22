# Windows-Service Installer
A CLI tool which Handles creating/deleting services.  
When both arguments are supplied the tool runs to completion.  
If less then two are supplied the tool runs interactive.  

It takes up to two optional arguments (in order).  
- The file-path of the .exe, which shall be started by the service.  
- What should happen to the service.  
    - "install": installs and starts the service.
    - "uninstall": stops and uninstalls the service.
