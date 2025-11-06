# My Azure AI Foundry client demo

## Setup and run the code

1. Create a Azure AI Foundry project if you don't have one already.
1. In the **Overview** section of your project, copy the Azure AI Foundry **project endpoint**. On your local machine, create a system/environment variable named `AI_PROJECT_ENDPOINT`. Set its value to the agent ID. Set its value to the **project endpoint**.
1. Create an agent an copy its ID in the Azure AI Foundry project. On your local machine (e.g., in your terminal, PowerShell, or environment variable settings), create a system/environment variable named `AI_AGENT_ID`. Set its value to the **agent ID**.
1. Sign in with the Azure CLI using the same account that you use to access your project. This is important to allow the `DefaultAzureCredential()` uses the correct tenant ID.
