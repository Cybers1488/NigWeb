# -*- coding: utf-8 -*-
import zlib

dmi_path = 'Z:/fork/OpenSourceWeb/icons/obj/surgery.dmi'

with open(dmi_path, 'rb') as f:
    data = f.read()

idx = data.find(b'zTXtDescription\x00')
chunk_data = data[idx+16:]
compressed = chunk_data[1:chunk_data.find(b'IEND')-8]
desc = zlib.decompress(compressed).decode('utf-8')

for line in desc.split('\n'):
    if line.startswith('state ='):
        print(line)
