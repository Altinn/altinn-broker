
# Broker frontend/"BrokerBox" (WORK-IN-PROGRESS)

This subfolder contains the code for the Broker frontend application that is intended to be linked to from Arbeidsflate, and permits organizations to easily and securely transfer large files to other organizations. It uses Broker TUS as its backend.

## Technology

It is a React app that directly connects to the Broker API from the browser, and uses ID-Porten for login.

## Goals

- [ ] The user should be able to login with ID-Porten
- [ ] The user should be able to create and upload a file transfer on behalf of their organization
- [ ] The recipient should get a notification about a new file transfer
- [ ] The recipient should be able to download the file
- [ ] There should be a progress bar displaying the progress
- [ ] The design should be consistent with the Arbeidsflate such that the user does not experience it as a distinct application
- [ ] The user should be able to see information and metadata about current and historical file transfers
- [ ] The UI should give information about Broker resources the user has access to
- [ ] It must be universally accessible for everyone regardless of disability
- [ ] The design should be responsive so that it can be used on all common devices
