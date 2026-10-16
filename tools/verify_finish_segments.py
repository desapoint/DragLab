"""Check actual Unity-rendered digits for joined segments at each supported capture size."""
from pathlib import Path
from collections import deque
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'reports/finish-segments-20261016'
EXPECTED = [6, 2, 5, 5, 4, 5, 6, 3, 7, 6]

def components(mask):
    pending = set(map(tuple, np.argwhere(mask)))
    count = 0
    while pending:
        count += 1
        queue = deque([pending.pop()])
        while queue:
            y, x = queue.popleft()
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    point = (y + dy, x + dx)
                    if point in pending:
                        pending.remove(point)
                        queue.append(point)
    return count

def validate():
    lines, failures = [], []
    for phase in ('before', 'after'):
        passed = 0
        for width in (1280, 1920, 3840):
            image = np.asarray(Image.open(ROOT / phase / f'digits-{width}.png').convert('RGB')).astype(float)
            height = image.shape[0]
            scale = 1.17 * height / 12.8
            for row, (bottom, digit_width, digit_height) in enumerate(((.1, .34, .63), (-.9, .29, .46))):
                for digit, expected in enumerate(EXPECTED):
                    left = -3 + .6 * digit
                    x0, x1 = int(width / 2 + (left - .02) * scale), int(np.ceil(width / 2 + (left + digit_width + .02) * scale))
                    y0, y1 = int(height / 2 - (bottom + digit_height + .02) * scale), int(np.ceil(height / 2 - (bottom - .02) * scale))
                    region = image[y0:y1, x0:x1]
                    mask = (region[:, :, 0] > 120) & (region[:, :, 0] > region[:, :, 1] * 1.3)
                    actual = components(mask)
                    ok = actual == expected
                    passed += ok
                    lines.append(f'{phase}: {width}px / {"ET" if row == 0 else "RT"} / {digit}: {actual}/{expected} separate lit segments / {"PASS" if ok else "JOINED OR MISSING"}')
                    if phase == 'after' and not ok:
                        failures.append(lines[-1])
        lines.append(f'{phase}: {passed}/60 rendered digit cases separated correctly')
    lines.append(f'Corrected captures: 60 cases / {len(failures)} failures')
    ROOT.mkdir(parents=True, exist_ok=True)
    (ROOT / 'verification.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(line for line in lines if 'cases' in line or line in failures))
    if failures:
        raise SystemExit(1)

if __name__ == '__main__':
    validate()
