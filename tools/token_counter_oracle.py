"""Regenerate fixed cl100k counts with the independent Python tiktoken 0.12.0."""
import importlib.metadata
import json
from pathlib import Path
import tiktoken

assert importlib.metadata.version('tiktoken') == '0.12.0'
texts = ['', 'hello world', 'a\nb', 'Hello, world!', 'One more sentence.',
         'https://example.com/verify', 'Español: café, niño. 中文。', 'مرحبا بالعالم',
         '日本語の文章です。', '🙂🚀', 'a\x00b', '<|endoftext|>', ' \t\r\n',
         'Hello, world!\nOne more sentence.']
enc = tiktoken.get_encoding('cl100k_base')
result = {'oracle': 'Python tiktoken 0.12.0', 'encoding': 'cl100k_base',
          'mode': 'encode_ordinary', 'cases': [{'text': t, 'count': len(enc.encode_ordinary(t))} for t in texts]}
path = Path(__file__).resolve().parents[1] / 'dotnet/OxidizePdf.NET.Tests/fixtures/token-counter-oracle.json'
path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print(path)
