'use client';

import { useEffect, useState } from 'react';
import { Paperclip, Share2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogTrigger } from '@/components/ui/dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip';

export function ShareDialog() {
  const [isShareDialogOpen, setIsShareDialogOpen] = useState(false);
  const [currentUrl, setCurrentUrl] = useState('');

  useEffect(() => {
    if (typeof window !== 'undefined') {
      setCurrentUrl(window.location.href);
    }
  }, []);

  const handleCopyLink = async () => {
    try {
      const url =
        typeof window !== 'undefined' ? window.location.href : currentUrl;
      await navigator.clipboard.writeText(url);
      toast.success('Link copied to clipboard!');
    } catch (error) {
      toast.error('Failed to copy link. Please try again.');
      console.error('Failed to copy:', error);
    }
  };

  const handleShareToX = () => {
    const url = encodeURIComponent(currentUrl || window.location.href);
    const text = encodeURIComponent('Check out this chat!');
    window.open(
      `https://twitter.com/intent/tweet?url=${url}&text=${text}`,
      '_blank',
    );
  };

  const handleShareToLinkedIn = () => {
    const url = encodeURIComponent(currentUrl || window.location.href);
    window.open(
      `https://www.linkedin.com/sharing/share-offsite/?url=${url}`,
      '_blank',
    );
  };

  const handleShareToReddit = () => {
    const url = encodeURIComponent(currentUrl || window.location.href);
    const title = encodeURIComponent('Check out this chat!');
    window.open(
      `https://reddit.com/submit?url=${url}&title=${title}`,
      '_blank',
    );
  };

  return (
    <Dialog open={isShareDialogOpen} onOpenChange={setIsShareDialogOpen}>
      <DialogTrigger asChild>
        <Button shape="circle" variant="mono">
          <Share2 />
          Share Chat
        </Button>
      </DialogTrigger>
      <DialogContent>
        {/* Share Options */}
        <div className="bg-background p-6 space-y-4">
          <div>
            <h3 className="text-lg font-semibold mb-2">Share Chat</h3>
            <p className="text-sm text-muted-foreground">
              Share this chat session with others
            </p>
          </div>

          <div className="grid grid-cols-4 gap-4">
            {/* Copy Link */}
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  shape="circle"
                  mode="icon"
                  className="size-12"
                  onClick={handleCopyLink}
                >
                  <Paperclip className="size-6 text-foreground" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                <p>Copy Link</p>
              </TooltipContent>
            </Tooltip>

            {/* X */}
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  shape="circle"
                  mode="icon"
                  className="size-12"
                  onClick={handleShareToX}
                >
                  <svg
                    viewBox="0 0 1200 1227"
                    className="size-6 text-foreground fill-current"
                  >
                    <path d="M714.163 519.284L1160.89 0H1055.03L667.137 450.887L357.328 0H0L468.492 681.821L0 1226.37H105.866L515.491 750.218L842.672 1226.37H1200L714.137 519.284H714.163ZM569.165 687.828L521.697 619.934L144.011 79.6944H306.615L611.412 515.685L658.88 583.579L1055.08 1150.3H892.476L569.165 687.854V687.828Z" />
                  </svg>
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                <p>X</p>
              </TooltipContent>
            </Tooltip>

            {/* LinkedIn */}
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  shape="circle"
                  mode="icon"
                  className="size-12"
                  onClick={handleShareToLinkedIn}
                >
                  <svg
                    viewBox="0 0 32 32"
                    className="size-6 text-foreground fill-current"
                  >
                    <path
                      fillRule="evenodd"
                      clipRule="evenodd"
                      d="M25.3333 4H6.66667C5.19333 4 4 5.19333 4 6.66667V25.3333C4 26.8067 5.19333 28 6.66667 28H25.3333C26.8067 28 28 26.8067 28 25.3333V6.66667C28 5.19333 26.8067 4 25.3333 4ZM11.2567 24.6667H7.67667V13.11H11.2567V24.6667ZM9.45 11.5967C8.28 11.5967 7.33333 10.6433 7.33333 9.46333C7.33333 8.28333 8.28 7.33 9.45 7.33C10.62 7.33 11.5667 8.28333 11.5667 9.46333C11.5667 10.6433 10.62 11.5967 9.45 11.5967ZM24.6667 24.6667H21.1067V18.6C21.1067 16.9367 20.4733 16.0067 19.16 16.0067C17.7267 16.0067 16.98 16.9733 16.98 18.6V24.6667H13.5467V13.11H16.98V14.6667C16.98 14.6667 18.0133 12.7567 20.4633 12.7567C22.9133 12.7567 24.67 14.2533 24.67 17.35V24.6667H24.6667Z"
                    />
                  </svg>
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                <p>LinkedIn</p>
              </TooltipContent>
            </Tooltip>

            {/* Reddit */}
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  shape="circle"
                  mode="icon"
                  className="size-12"
                  onClick={handleShareToReddit}
                >
                  <svg
                    viewBox="0 0 24 24"
                    className="size-6 text-foreground fill-current"
                  >
                    <path d="M12 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0zm5.01 4.744c.688 0 1.25.561 1.25 1.249a1.25 1.25 0 0 1-2.498.056l-2.597-.547-.8 3.747c1.824.07 3.48.632 4.674 1.488.308-.309.73-.491 1.207-.491.968 0 1.754.786 1.754 1.754 0 .716-.435 1.333-1.01 1.614a3.111 3.111 0 0 1 .042.52c0 2.694-3.13 4.87-7.004 4.87-3.874 0-7.004-2.176-7.004-4.87 0-.183.015-.366.043-.534A1.748 1.748 0 0 1 4.028 12c0-.968.786-1.754 1.754-1.754.463 0 .898.196 1.207.49 1.207-.883 2.878-1.43 4.744-1.487l.885-4.182a.342.342 0 0 1 .14-.197.35.35 0 0 1 .238-.042l2.906.617a1.214 1.214 0 0 1 1.108-.701zM9.25 12C8.561 12 8 12.562 8 13.25c0 .687.561 1.248 1.25 1.248.687 0 1.248-.561 1.248-1.249 0-.688-.561-1.249-1.249-1.249zm5.5 0c-.687 0-1.248.561-1.248 1.25 0 .687.561 1.248 1.249 1.248.688 0 1.249-.561 1.249-1.249 0-.687-.562-1.249-1.25-1.249zm-5.466 3.99a.327.327 0 0 0-.231.094.33.33 0 0 0 0 .463c.842.842 2.484.913 3.4.913.917 0 2.558-.07 3.4-.913a.331.331 0 0 0 .195-.527.326.326 0 0 0-.464 0c-.547.547-1.684.73-2.13.73-.447 0-1.583-.183-2.13-.73a.326.326 0 0 0-.235-.095z" />
                  </svg>
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                <p>Reddit</p>
              </TooltipContent>
            </Tooltip>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
