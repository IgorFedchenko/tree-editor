import type { ReactNode } from 'react';

type Props = {
  testId: string;
  selected: boolean;
  onSelect: () => void;
  row: ReactNode;
  children?: ReactNode;
};

export default function TreeNode({ testId, selected, onSelect, row, children }: Props) {
  return (
    <li data-testid={testId} className={selected ? 'active' : ''}>
      <div
        className={'tree-row d-flex align-items-center rounded px-2 py-1' + (selected ? ' bg-primary text-white' : '')}
        onClick={onSelect}
      >
        {row}
      </div>
      {children && <ul className="list-unstyled ms-4 mb-0">{children}</ul>}
    </li>
  );
}
