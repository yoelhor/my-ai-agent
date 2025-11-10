using Azure;
using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;
using System.Net.Http.Headers;

async Task RunAgentConversation()
{
    var projectEndpoint = System.Environment.GetEnvironmentVariable("AI_PROJECT_ENDPOINT");
    if (string.IsNullOrEmpty(projectEndpoint))
    {
        Console.WriteLine("Please set the AI_PROJECT_ENDPOINT environment variable.");
        return;
    }

    var agentId = System.Environment.GetEnvironmentVariable("AI_AGENT_ID");
    if (string.IsNullOrEmpty(agentId))
    {
        Console.WriteLine("Please set the AI_AGENT_ID environment variable.");
        return;
    }

    var endpoint = new Uri(projectEndpoint);
    var credential = new DefaultAzureCredential();
    
    // Get the access token for Azure Management API
    var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" });
    var accessToken = await credential.GetTokenAsync(tokenRequestContext);
    
    AIProjectClient projectClient = new(endpoint, credential);

    PersistentAgentsClient agentsClient = projectClient.GetPersistentAgentsClient();
    PersistentAgent agent;
    try
    {
        agent = agentsClient.Administration.GetAgent(agentId);
    }
    catch (RequestFailedException ex) when (ex.Status == 404)
    {
        Console.WriteLine(ex.Message);
        return;
    }

    PersistentAgentThread thread = agentsClient.Threads.CreateThread();
    Console.WriteLine($"Created thread, ID: {thread.Id}");

    // Send initial message to the agent
    Console.Write("Please enter your prompt: ");

    string inputPrompt = Console.ReadLine() ?? $"Echo back message : {DateTime.Now.ToString()}.";
    PersistentThreadMessage messageResponse = agentsClient.Messages.CreateMessage(
        thread.Id,
        MessageRole.User,
        inputPrompt);

    // Create run with tool resources to pass the access token to MCP server
    MCPToolResource mcpToolResource = new("azure");
    mcpToolResource.UpdateHeader("Authorization", $"Bearer {accessToken.Token}");

    // Add the session ID header if available
    if (!string.IsNullOrEmpty(thread.Id))
    {
        mcpToolResource.UpdateHeader("x-custom-thread-id", thread.Id);
    }
    
    mcpToolResource.RequireApproval = new MCPApproval("never");
    ToolResources toolResources = mcpToolResource.ToToolResources();

    ThreadRun run = agentsClient.Runs.CreateRun(thread, agent, toolResources);

    // Poll until the run reaches a terminal status
    while (run.Status == RunStatus.Queued 
        || run.Status == RunStatus.InProgress 
        || run.Status == RunStatus.RequiresAction)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        run = agentsClient.Runs.GetRun(thread.Id, run.Id);

        // Handle MCP tool approval requests
        if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
        {
            var toolApprovals = new List<ToolApproval>();
            foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
            {
                if (toolCall is RequiredMcpToolCall mcpToolCall)
                {
                    Console.WriteLine($"Approving MCP tool call: {mcpToolCall.Name}");
                    Console.WriteLine($"Arguments: {mcpToolCall.Arguments}");
                    
                    toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true)
                    {
                        // Add the session ID and the authorization header
                        Headers = 
                        {
                            ["x-custom-thread-id"] = thread.Id,
                            ["Authorization"] = $"Bearer {accessToken.Token}"
                        }
                    });
                }
            }

            if (toolApprovals.Count > 0)
            {
                run = agentsClient.Runs.SubmitToolOutputsToRun(thread.Id, run.Id, toolApprovals: toolApprovals);
            }
        }
    }
    
    if (run.Status != RunStatus.Completed)
    {
        Console.WriteLine("Run did not complete successfully.");
        Console.WriteLine($"Run Status: {run.Status}");
        Console.WriteLine($"Run Error: {run.LastError?.Message}");
        throw new InvalidOperationException($"Run failed or was canceled: {run.LastError?.Message}");
    }

    Pageable<PersistentThreadMessage> messages = agentsClient.Messages.GetMessages(
        thread.Id, order: ListSortOrder.Ascending);

    // Display messages
    foreach (PersistentThreadMessage threadMessage in messages)
    {
        // Display message header make the first letter of the role uppercase
        Console.Write($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {char.ToUpper(threadMessage.Role.ToString()[0])}{threadMessage.Role.ToString().Substring(1)}: ");

        foreach (MessageContent contentItem in threadMessage.ContentItems)
        {
            if (contentItem is MessageTextContent textItem)
            {
                // Display text content
                Console.Write(textItem.Text);
            }
            else if (contentItem is MessageImageFileContent imageFileItem)
            {
                Console.Write($"<image from ID: {imageFileItem.FileId}");
            }
            Console.WriteLine();
        }
    }
}

// Main execution
await RunAgentConversation();