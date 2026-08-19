import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { AI_MODELS } from '../../../../app/ai/mock/ai-models';
import { SectionHeader } from './section-header';

export function AIModelSelector({ selectedModel, onModelSelect }) {
  return (
    <div className="space-y-2">
      <SectionHeader label="AI Model" />
      <div className="grid grid-cols-3 gap-2">
        {AI_MODELS.map((model) => {
          const Icon = model.icon;
          const isSelected = selectedModel === model.id;
          return (
            <Button
              key={model.id}
              variant="outline"
              size="sm"
              autoHeight
              className={cn(
                'flex-col gap-1 py-2',
                isSelected && 'bg-muted shadow-lg shadow-black/5',
              )}
              onClick={() => onModelSelect(model.id)}
            >
              <Icon className={cn('size-4', model.color)} />
              <span className="text-xs leading-tight">{model.name}</span>
            </Button>
          );
        })}
      </div>
    </div>
  );
}
