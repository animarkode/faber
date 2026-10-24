import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { type VariantProps, cva } from 'class-variance-authority';
import { cn } from '../../utils/cn';

const iconButtonVariants = cva(
  'inline-flex items-center justify-center cursor-pointer transition-all focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
  {
    variants: {
      intent: {
        default:
          'rounded-[10px] bg-transparent text-muted-foreground hover:bg-input hover:text-foreground',
        ghost:
          'rounded-[9px] bg-transparent text-muted-foreground/70 hover:bg-muted hover:text-foreground',
        // Filled with the card colour by default. A container that is itself
        // bg-card sets --icon-button-surface (fb-auth-card does), or the fill
        // would vanish into it.
        surface:
          'rounded-[1.25rem] bg-[var(--icon-button-surface,var(--color-card))] text-foreground border-[0.5px] border-line hover:rounded-xl',
        // Mirrors fb-button's `soft-destructive`: the destructive colours live
        // inside the variant so callers never layer competing `text-*` classes
        // on top of `default`'s `text-muted-foreground` (#406). The hover fill
        // uses the opaque destructive-soft token pair, not an alpha
        // `destructive/10` fill, which composites to plain gray on dark cards (#370).
        destructive:
          'rounded-full bg-transparent text-destructive-text hover:bg-destructive-soft hover:text-destructive-text',
      },
      size: {
        sm: 'h-8 w-8',
        md: 'h-9 w-9',
        lg: 'h-10 w-10',
      },
    },
    defaultVariants: {
      intent: 'default',
      size: 'md',
    },
  }
);

export type IconButtonVariants = VariantProps<typeof iconButtonVariants>;

@Component({
  selector: '[fb-icon-button]',
  templateUrl: './fb-icon-button.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': 'classes()',
  },
})
export class FbIconButton {
  readonly intent = input<IconButtonVariants['intent']>('default');
  readonly size = input<IconButtonVariants['size']>('md');
  readonly class = input<string>('');

  protected readonly classes = computed(() =>
    cn(iconButtonVariants({ intent: this.intent(), size: this.size() }), this.class())
  );
}
