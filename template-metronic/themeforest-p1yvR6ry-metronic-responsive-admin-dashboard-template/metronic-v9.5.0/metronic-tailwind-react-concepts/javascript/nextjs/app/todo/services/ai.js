const AI_API_URL =
  process.env.NEXT_PUBLIC_AI_API_URL ||
  'https://api.openai.com/v1/chat/completions';
const AI_API_KEY = process.env.NEXT_PUBLIC_AI_API_KEY || '';
const AI_MODEL = process.env.NEXT_PUBLIC_AI_MODEL || 'gpt-4o-mini';

const SYSTEM_PROMPT = `You are a helpful AI assistant for task management and productivity. Your role is to:

1. Help users prioritize their tasks based on urgency and importance
2. Break down complex tasks into smaller, actionable steps
3. Provide time estimates for tasks
4. Suggest productivity tips and quick wins
5. Offer motivational support and guidance

Keep your responses concise, actionable, and friendly. Use markdown formatting when helpful (lists, bold, headers). 
Focus on being practical and results-oriented. When analyzing tasks, consider deadlines, dependencies, and effort required.`;

export async function sendMessage(messages, onChunk) {
  if (!AI_API_KEY) {
    throw new Error(
      'AI API key is not configured. Please set NEXT_PUBLIC_AI_API_KEY in your environment.',
    );
  }

  const response = await fetch(AI_API_URL, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${AI_API_KEY}`,
    },
    body: JSON.stringify({
      model: AI_MODEL,
      messages: [{ role: 'system', content: SYSTEM_PROMPT }, ...messages],

      stream: !!onChunk,
      temperature: 0.7,
      max_tokens: 1024,
    }),
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(`AI API error: ${response.status} - ${error}`);
  }

  // Non-streaming response
  if (!onChunk) {
    const data = await response.json();
    return data.choices[0]?.message?.content || '';
  }

  // Streaming response
  const reader = response.body?.getReader();
  const decoder = new TextDecoder();
  let fullContent = '';

  if (!reader) {
    throw new Error('No response body');
  }

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    const chunk = decoder.decode(value);
    const lines = chunk.split('\n').filter((line) => line.trim() !== '');

    for (const line of lines) {
      if (line.startsWith('data: ')) {
        const data = line.slice(6);
        if (data === '[DONE]') continue;

        try {
          const parsed = JSON.parse(data);
          const content = parsed.choices[0]?.delta?.content || '';
          if (content) {
            fullContent += content;
            onChunk(content);
          }
        } catch {
          // Ignore parsing errors for incomplete chunks
        }
      }
    }
  }

  return fullContent;
}

export async function sendMessageWithFallback(messages, onChunk) {
  // If no API key is configured, use a demo mode with simulated responses
  if (!AI_API_KEY) {
    return simulateResponse(messages, onChunk);
  }

  return sendMessage(messages, onChunk);
}

async function simulateResponse(messages, onChunk) {
  const lastMessage =
    messages[messages.length - 1]?.content.toLowerCase() || '';

  const responses = {
    prioritize: `## Task Prioritization

Based on your workload, here's my recommended approach:

### 🔴 High Priority (Do First)
1. **Urgent deadlines** - Tasks due today or tomorrow
2. **Blocking tasks** - Items that others depend on
3. **High-impact items** - Quick wins that unlock more progress

### 🟡 Medium Priority (Schedule)
- Tasks with flexible deadlines
- Important but not urgent work
- Planning and preparation activities

### 🟢 Low Priority (Delegate or Defer)
- Nice-to-have improvements
- Long-term projects without deadlines

Would you like me to help categorize your specific tasks?`,

    time: `## Time Estimation Guide

Here's how I estimate task duration:

| Task Type | Typical Duration |
|-----------|-----------------|
| Quick fixes | 5-15 minutes |
| Simple tasks | 15-30 minutes |
| Medium tasks | 30-60 minutes |
| Complex tasks | 1-2 hours |
| Deep work | 2-4 hours |

### Tips for Better Estimates
- Add 20% buffer for unexpected issues
- Break large tasks into smaller chunks
- Track actual time to improve future estimates

Tell me about your tasks and I'll help estimate them!`,

    break: `## Breaking Down Complex Tasks

Here's my 4-step framework:

### 1. 🎯 Define the Outcome
*What does "done" look like?*

### 2. 🔗 Identify Dependencies
*What needs to happen first?*

### 3. 📝 Create Subtasks
*Break into 30-minute chunks*

### 4. ✅ Set Milestones
*Add checkpoints for progress*

**Example:**
- Complex task: "Redesign homepage"
  - Research competitors (1h)
  - Sketch wireframes (30m)
  - Create mockups (2h)
  - Get feedback (30m)
  - Implement changes (3h)

Which task would you like me to break down?`,

    quick: `## Quick Wins Available 🚀

Here are tasks you can knock out in 15 minutes or less:

✅ **Administrative**
- Clear inbox to zero
- Update task statuses
- Schedule tomorrow's priorities

✅ **Communication**
- Send quick follow-up emails
- Reply to pending messages
- Add notes to recent meetings

✅ **Organization**
- Archive completed tasks
- Tag unorganized items
- Review weekly calendar

**Pro tip:** Batch similar quick tasks together for even greater efficiency!

Ready to tackle some quick wins?`,
  };

  let response = `## How Can I Help?

I'm your AI productivity assistant! I can help you with:

- **📋 Task Prioritization** - Sort your tasks by importance and urgency
- **⏱️ Time Estimates** - Predict how long tasks will take
- **🧩 Task Breakdown** - Split complex projects into steps
- **⚡ Quick Wins** - Find fast tasks to build momentum

What would you like to work on today?`;

  if (lastMessage.includes('priorit')) {
    response = responses.prioritize;
  } else if (
    lastMessage.includes('time') ||
    lastMessage.includes('estimate') ||
    lastMessage.includes('long')
  ) {
    response = responses.time;
  } else if (
    lastMessage.includes('break') ||
    lastMessage.includes('step') ||
    lastMessage.includes('complex')
  ) {
    response = responses.break;
  } else if (
    lastMessage.includes('quick') ||
    lastMessage.includes('fast') ||
    lastMessage.includes('win')
  ) {
    response = responses.quick;
  }

  // Simulate streaming
  if (onChunk) {
    const words = response.split(' ');
    for (let i = 0; i < words.length; i++) {
      await new Promise((resolve) =>
        setTimeout(resolve, 20 + Math.random() * 30),
      );
      onChunk(words[i] + (i < words.length - 1 ? ' ' : ''));
    }
  } else {
    await new Promise((resolve) =>
      setTimeout(resolve, 500 + Math.random() * 500),
    );
  }

  return response;
}
