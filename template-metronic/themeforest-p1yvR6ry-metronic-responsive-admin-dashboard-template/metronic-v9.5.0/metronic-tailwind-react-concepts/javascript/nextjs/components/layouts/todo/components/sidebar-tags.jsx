import { useState } from 'react';
import { Plus } from 'lucide-react';
import { BadgeDot } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { defaultTags } from '@/app/todo/mock';
import { AddTagDialog } from './add-tag';

export function SidebarTags() {
  const [tags, setTags] = useState(defaultTags);
  const [isDialogOpen, setIsDialogOpen] = useState(false);

  const handleCreateTag = (data) => {
    const newTag = {
      id: Date.now().toString(),
      name: data.name,
      color: data.color,
    };
    setTags([...tags, newTag]);
  };

  return (
    <>
      <h4 className="text-xs px-2 mb-2 text-muted-foreground">Tags</h4>
      <div className="flex flex-wrap gap-2">
        {tags.map((tag) => (
          <Button key={tag.id} size="sm" variant="outline">
            <BadgeDot style={{ backgroundColor: tag.color }} />
            {tag.name}
          </Button>
        ))}

        <Button
          variant="outline"
          size="sm"
          onClick={() => setIsDialogOpen(true)}
        >
          <Plus className="size-4" />
          Add tag
        </Button>
      </div>

      <AddTagDialog
        open={isDialogOpen}
        onOpenChange={setIsDialogOpen}
        onCreate={handleCreateTag}
      />
    </>
  );
}
