import { Brain, Cpu, Sparkles } from "lucide-react";
import { AIModel } from "../types";

export const AI_MODELS: AIModel[] = [
  { id: "gpt-4", name: "GPT-4", icon: Brain, color: "text-purple-500" },
  { id: "claude", name: "Claude", icon: Cpu, color: "text-blue-500" },
  { id: "gemini", name: "Gemini", icon: Sparkles, color: "text-orange-500" },
];
