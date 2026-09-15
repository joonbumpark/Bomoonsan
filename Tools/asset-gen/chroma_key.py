#!/usr/bin/env python3
"""마젠타(#FF00FF) 배경으로 뽑은 이미지에서 배경만 투명하게 만든다.

gpt-image-1에 background=transparent를 그대로 요청하면, 흰색으로 채운
오브젝트 내부까지 "배경"으로 오인해서 같이 지워버리는 문제가 있었다.
그래서 대신 불투명한 마젠타 배경으로 생성한 뒤, 여기서 마젠타에 가까운
픽셀만 크로마키로 제거해서 흰색 채우기는 그대로 보존한다.
"""

import sys
from PIL import Image

MAGENTA = (255, 0, 255)
# 이 거리 이내면 배경으로 간주해서 투명화한다. 안티에일리어싱 경계는
# 부분 투명도(soft alpha)로 자연스럽게 처리한다.
FULL_TRANSPARENT_DIST = 130
FULL_OPAQUE_DIST = 220


def distance(px, target):
    return sum((a - b) ** 2 for a, b in zip(px[:3], target)) ** 0.5


def chroma_key(in_path, out_path):
    img = Image.open(in_path).convert('RGBA')
    pixels = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            d = distance((r, g, b), MAGENTA)
            if d <= FULL_TRANSPARENT_DIST:
                pixels[x, y] = (r, g, b, 0)
            elif d < FULL_OPAQUE_DIST:
                # 경계 부분은 선형 보간으로 부드럽게 투명도를 준다.
                t = (d - FULL_TRANSPARENT_DIST) / (FULL_OPAQUE_DIST - FULL_TRANSPARENT_DIST)
                pixels[x, y] = (r, g, b, int(255 * t))
    img.save(out_path)


if __name__ == '__main__':
    if len(sys.argv) != 3:
        print('사용법: chroma_key.py <입력.png> <출력.png>')
        sys.exit(1)
    chroma_key(sys.argv[1], sys.argv[2])
    print(f'저장됨: {sys.argv[2]}')
