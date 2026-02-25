# Graph Editor
[![GitHub Release](https://img.shields.io/github/v/release/rykoma/Graph-Editor?style=flat&logo=GitHub)](../../releases)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/rykoma/Graph-Editor/main.yml?style=flat&logo=GitHub)](../../actions)
[![GitHub License](https://img.shields.io/github/license/rykoma/Graph-Editor?style=flat&logo=GitHub)](./LICENSE)

Graph Editor is your go-to tool for easily experimenting with Microsoft Graph. It beautifully displays requests and responses, making it easier for you to understand how Microsoft Graph works.

Within Graph Editor, you can manage your execution history, allowing you to effortlessly compare various requests and responses. Plus, with a rich set of sample queries, you can send a variety of requests with ease.

![image](https://github.com/user-attachments/assets/3223705c-6c48-4b5c-915a-456660996df5)

## Download options

Click the badge below to download the installer directly:

<a href="https://get.microsoft.com/installer/download/9P7TCMNQ7ZHB?referrer=appbadge" target="_self" >
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

You can also [visit the Microsoft Store page](https://apps.microsoft.com/detail/9P7TCMNQ7ZHB) to view more details.

## Features

### Editor

You can send Microsoft Graph requests and see the responses.

### History

You can see the history of the requests you have sent.

### Sample Query

You can see the sample queries for Microsoft Graph.

### Access Token

You can use the built-in application ID to get the access token. You can also use your own application ID.

## Known issues

### Sample query with unimplemented data

Some sample queries are not yet fully implemented. We are systematically implementing sample queries based on the Microsoft Graph API reference, with new queries added in each release.

**What you can do:** Use the main editor to send any Microsoft Graph request manually. Enter the endpoint URL, select the HTTP method, and configure headers and body as needed.

### App Library in Settings page is not available

The App Library feature for managing multiple Microsoft Entra ID application registrations is under development and will be available in a future update.

**What you can do:** Manually enter your application credentials in the Access Token wizard. Consider documenting your application details in a secure location for quick reference.

## Third-party libraries

This application includes the following third-party library:

- WinUIEdit  
  Copyright 2025 by Breece Walker
  See [https://github.com/BreeceW/WinUIEdit/blob/main/LICENSE](https://github.com/BreeceW/WinUIEdit/blob/main/LICENSE).

## Feedback

If you have any feedback, please post it on the [issues](https://github.com/rykoma/Graph-Editor/issues) page.
