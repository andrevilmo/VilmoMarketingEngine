export const DEFAULT_USER_PROMPT = 'How to make cheesecake';

export const DEFAULT_ASSISTANT_RESPONSE = `I'd be happy to help you make a cheesecake! Here's a classic recipe:
  **Classic Cheesecake**

  **For the crust:**
  • 1 1/2 cups graham cracker crumbs
  • 1/4 cup granulated sugar
  • 1/2 cup unsalted butter, melted

  **For the filling:**
  • 4 (8-ounce) packages cream cheese, softened
  • 1 1/4 cups granulated sugar
  • 1/2 cup sour cream
  • 2 teaspoons vanilla extract
  • 4 large eggs

  **Instructions:**

  **Crust:**
  1. Preheat oven to 325°F (165°C).
  2. Mix graham cracker crumbs, sugar, and melted butter.
  3. Press into the bottom of a 9-inch springform pan.
  4. Bake for 10 minutes, then cool.

  **Filling:**
  1. Beat cream cheese and sugar until smooth.
  2. Add sour cream and vanilla, mix well.
  3. Add eggs one at a time, mixing just until blended.
  4. Pour over crust and bake for 55-60 minutes.

  **Baking:**
  • Bake until center is almost set but still slightly jiggly.
  • Turn off oven and let cool in oven with door slightly open for 1 hour.

  **Cooling:**
  • Remove from oven and cool completely.
  • Refrigerate for at least 4 hours or overnight before serving.

  Would you like tips for specific flavors (chocolate, strawberry, etc.) or have questions about this recipe?`;

export const createInitialMessages = () => {
  const timestamp = new Date().toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
  });

  const userMessage = {
    id: 'user-initial',
    role: 'user',
    content: DEFAULT_USER_PROMPT,
    timestamp,
  };

  const assistantMessage = {
    id: 'assistant-initial',
    role: 'assistant',
    content: DEFAULT_ASSISTANT_RESPONSE,
    timestamp,
  };

  return [userMessage, assistantMessage];
};

export const getMessagesForChat = (chatId) => {
  switch (chatId) {
    case '1':
      return [
        {
          id: '1',
          role: 'user',
          content:
            'I need help planning a website redesign for a tech startup.',
          timestamp: '10:00 AM',
        },
        {
          id: '2',
          role: 'assistant',
          content:
            'I can certainly help with that. To get started, could you tell me a bit more about the startup? What are their main goals for the redesign, and who is their target audience?',
          timestamp: '10:01 AM',
        },
        {
          id: '3',
          role: 'user',
          content:
            'They focus on AI-driven analytics. The goal is to look more modern and trustworthy. Target audience is enterprise CTOs.',
          timestamp: '10:02 AM',
        },
        {
          id: '4',
          role: 'assistant',
          content:
            "Got it. For an AI analytics firm targeting enterprise CTOs, the design should prioritize clarity, professionalism, and data visualization. \n\nHere's a high-level plan:\n\n1. **Discovery Phase**: Analyze competitors and current user behavior.\n2. **UX Strategy**: Create user personas (CTOs) and map out user journeys focusing on ease of accessing insights.\n3. **Visual Identity**: Use a clean, modern color palette (blues, teals, dark mode options) with high-quality abstract 3D graphics representing data.\n4. **Content Strategy**: Focus on case studies, whitepapers, and clear value propositions.\n5. **Technical Stack**: Ensure high performance and security compliance.\n\nWould you like to dive deeper into any of these phases?",
          timestamp: '10:03 AM',
        },
      ];

    case '2': // What to wear kayaking
      return [
        {
          id: '2-1',
          role: 'user',
          content: 'What should I wear for kayaking?',
          timestamp: '1 hour ago',
        },
        {
          id: '2-2',
          role: 'assistant',
          content:
            'For kayaking, you should dress for the water temperature, not the air temperature. \n\n**Essentials:**\n• PFD (Life jacket)\n• Wetsuit or drysuit (if cold)\n• Quick-drying synthetic layers\n• Water shoes\n• Hat and sunglasses',
          timestamp: '55 min ago',
        },
      ];

    case '3': // Redux code example
      return [
        {
          id: '3-1',
          role: 'user',
          content: 'Show me a simple Redux slice example.',
          timestamp: '3 hours ago',
        },
        {
          id: '3-2',
          role: 'assistant',
          content:
            "Sure! Here is a simple Redux slice using Redux Toolkit:\n\n```typescript\nimport { createSlice, PayloadAction } from '@reduxjs/toolkit';\n\ninterface CounterState {\n  value: number;\n}\n\nconst initialState: CounterState = {\n  value: 0,\n};\n\nexport const counterSlice = createSlice({\n  name: 'counter',\n  initialState,\n  reducers: {\n    increment: (state) => {\n      state.value += 1;\n    },\n    decrement: (state) => {\n      state.value -= 1;\n    },\n    incrementByAmount: (state, action: PayloadAction<number>) => {\n      state.value += action.payload;\n    },\n  },\n});\n\nexport const { increment, decrement, incrementByAmount } = counterSlice.actions;\nexport default counterSlice.reducer;\n```",
          timestamp: '3 hours ago',
        },
      ];

    default:
      return createInitialMessages();
  }
};
