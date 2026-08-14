import {
  Box,
  Briefcase,
  Code,
  GraduationCap,
  Infinity,
  Layers,
  Lightbulb,
  MessageSquare,
  Moon,
  Palette,
  Rocket,
  UserCog,
  Zap,
} from 'lucide-react';

export const CHAT_STARTER_MODEL_OPTIONS = [
  {
    id: 'auto',
    name: 'Auto',
    icon: Rocket,
    description: 'Chooses Fast or Expert',
  },
  { id: 'fast', name: 'Fast', icon: Zap, description: 'Quick responses' },
  { id: 'expert', name: 'Expert', icon: Lightbulb, description: 'Thinks hard' },
  { id: 'grok-4.1', name: 'Grok 4.1', icon: Moon, description: 'Beta' },
  { id: 'heavy', name: 'Heavy', icon: Layers, description: 'Team of experts' },
  {
    id: 'super-grok',
    name: 'SuperGrok',
    icon: Infinity,
    description: 'Unlock extended capabilities',
    upgrade: true,
  },
  {
    id: 'custom-instructions',
    name: 'Custom Instructions',
    icon: UserCog,
    description: 'Not set',
    customize: true,
  },
  {
    id: 'all-models',
    name: 'All Models',
    icon: Box,
    description: '',
    locked: true,
  },
];

export const CHAT_STARTER_PERSONAS = [
  {
    id: 'developer',
    name: 'Developer',
    icon: Code,
    description: 'Code assistance and technical help',
  },
  {
    id: 'business',
    name: 'Business',
    icon: Briefcase,
    description: 'Business strategy and planning',
  },
  {
    id: 'educator',
    name: 'Educator',
    icon: GraduationCap,
    description: 'Teaching and learning support',
  },
  {
    id: 'creative',
    name: 'Creative',
    icon: Palette,
    description: 'Creative writing and ideas',
  },
  {
    id: 'support',
    name: 'Support',
    icon: MessageSquare,
    description: 'Customer support assistance',
  },
];
