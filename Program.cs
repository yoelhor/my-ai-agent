using Azure;
using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;

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
    AIProjectClient projectClient = new(endpoint, new DefaultAzureCredential());

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
    string inputPrompt = Console.ReadLine() ?? "Hello, Agent!";
    PersistentThreadMessage messageResponse = agentsClient.Messages.CreateMessage(
        thread.Id,
        MessageRole.User,
        inputPrompt);

    ThreadRun run = agentsClient.Runs.CreateRun(
        thread.Id,
        agent.Id);

    // Poll until the run reaches a terminal status
    do
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        run = agentsClient.Runs.GetRun(thread.Id, run.Id);
    }
    while (run.Status == RunStatus.Queued
        || run.Status == RunStatus.InProgress);
    if (run.Status != RunStatus.Completed)
    {
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