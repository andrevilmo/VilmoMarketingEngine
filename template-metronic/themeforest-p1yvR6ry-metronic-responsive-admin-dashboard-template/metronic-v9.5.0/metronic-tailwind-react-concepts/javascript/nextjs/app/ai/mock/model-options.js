import { Brain, Code2, Workflow } from 'lucide-react';

export const MODEL_OPTIONS = [
  {
    id: 'gpt-4',
    label: 'GPT-4',
    tagline: 'Smarter and faster',
    description:
      'Flagship multimodal model for reasoning-heavy chat, voice, and vision.',
    icon: Brain,
    badge: { label: 'Default', variant: 'info' },
  },
  {
    id: 'gpt-4-mini',
    label: 'GPT-4 mini',
    tagline: 'Quick drafts & prototyping',
    description:
      'Lightweight and cost-efficient for rapid iteration across teams.',
    icon: Workflow,
    badge: { label: 'Fast', variant: 'success' },
  },
  {
    id: 'gpt-4-turbo',
    label: 'GPT-4 Turbo',
    tagline: 'Developer ready',
    description:
      'Stable API surface with JSON mode, structured output, and tools.',
    icon: Code2,
  },
];
