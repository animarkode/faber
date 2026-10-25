import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { type VariantProps, cva } from 'class-variance-authority';
import { cn } from '../../utils/cn';

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 rounded-full font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:pointer-events-none disabled:opacity-65 cursor-pointer',
  {
    variants: {
      variant: {
        default: 'before:bg-muted text-foreground hover:before:bg-surface hover:before:scale-[0.975]',
        primary: 'before:bg-primary text-primary-foreground hover:before:scale-[0.975]',
        outline: 'border border-line text-foreground hover:before:bg-muted hover:before:scale-[0.975]',
        ghost: 'text-foreground hover:before:bg-muted hover:before:scale-[0.975]',
        destructive: 'before:bg-destructive text-destructive-foreground hover:before:bg-destructive-hover hover:before:scale-[0.975]',
        'soft-destructive': 'before:bg-destructive-soft text-destructive-text hover:before:bg-destructive-soft-hover hover:before:scale-[0.975]',
        link: 'text-primary underline-offset-4 hover:underline p-0 h-auto',
        // Same look and hover as the links in the app navbar.
        menu: 'before:bg-surface text-foreground/80 hover:before:scale-[0.955] hover:before:bg-[color-mix(in_oklch,var(--color-surface)_80%,var(--color-foreground))] focus-visible:before:scale-[0.955] focus-visible:before:bg-[color-mix(in_oklch,var(--color-surface)_80%,var(--color-foreground))]',
      },
      size: {
        sm: 'h-8 px-3 text-sm',
        md: 'h-10 px-4 text-sm',
        lg: 'h-12 px-6 text-base',
        icon: 'h-9 w-9',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'md',
    },
  }
);

export type ButtonVariants = VariantProps<typeof buttonVariants>;

@Component({
  selector: 'fb-button, [fb-button]',
  templateUrl: './fb-button.html',
  styleUrl: './fb-button.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': 'classes()',
  },
})
export class FbButton {
  readonly variant = input<ButtonVariants['variant']>('default');
  readonly size = input<ButtonVariants['size']>('md');
  readonly class = input<string>('');

  protected readonly classes = computed(() =>
    cn(buttonVariants({ variant: this.variant(), size: this.size() }), this.class())
  );
}
