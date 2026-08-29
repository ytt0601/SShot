# Third-Party Notices

SShot 本体は [LICENSE](./LICENSE) の MIT ライセンスですが、配布物には以下のサードパーティ製
ソフトウェアを**同梱**しています。ポータブル版 `SShot.App.exe` は自己完結型の単一ファイル発行
(`PublishSingleFile` + `--self-contained`) であり、.NET ランタイムを含む下記すべてがこの1ファイルに
埋め込まれています。MSI 版も同一の exe を配置するため対象は同じです。

以下は各ライセンスが要求する著作権表示およびライセンス条項の再頒布です。

---

## 同梱コンポーネント一覧

| Component | Version | License | Copyright |
| --- | --- | --- | --- |
| .NET Runtime / Windows Desktop Runtime (WPF) | 10.0.4 | MIT | .NET Foundation and Contributors |
| SkiaSharp | 4.148.0 | MIT | Xamarin, Inc. / Microsoft Corporation |
| SkiaSharp.NativeAssets.Win32 (`libSkiaSharp.dll`) | 4.148.0 | MIT（内包するネイティブ部品は別掲） | Xamarin, Inc. / Microsoft Corporation |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | .NET Foundation and Contributors |
| Microsoft.Extensions.DependencyInjection<br>Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.9 | MIT | .NET Foundation and Contributors |
| Hardcodet.NotifyIcon.Wpf | 2.0.1 | MIT | Philipp Sumi |
| NHotkey / NHotkey.Wpf | 4.0.0 | Apache-2.0 | Thomas Levesque |

いずれも改変せずそのまま利用しています（Apache-2.0 §4(b) にいう変更は加えていません）。

ビルド時のみ使用され配布物に含まれないもの（`Microsoft.NET.ILLink.Tasks`、xUnit および関連テスト
ツール）と、win-x64 発行では除外される他 RID 向けネイティブ資産（`SkiaSharp.NativeAssets.macOS`）は
上表の対象外です。

長大な告知文は `licenses/` 配下に原文のまま収録しています。

- [`licenses/Apache-2.0.txt`](./licenses/Apache-2.0.txt) — Apache License 2.0 全文
- [`licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt`](./licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt) — .NET ランタイムが内包する第三者部品の告知（Microsoft 提供の原文）
- [`licenses/skiasharp-native-THIRD-PARTY-NOTICES.txt`](./licenses/skiasharp-native-THIRD-PARTY-NOTICES.txt) — `libSkiaSharp.dll` が内包する第三者部品の告知（Skia、libpng、libjpeg-turbo、Adobe DNG SDK ほか。SkiaSharp 提供の原文）

---

## .NET Runtime / Windows Desktop Runtime

Self-contained 発行のため、.NET 10 ランタイムおよび WPF を含む Windows Desktop ランタイムを
同梱しています。ランタイム自身が内包する第三者部品の告知は
[`licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt`](./licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt)
を参照してください。

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## SkiaSharp / SkiaSharp.NativeAssets.Win32

モザイク・ぼかしフィルタの畳み込み処理に使用しています。

ネイティブライブラリ `libSkiaSharp.dll` は Skia をはじめとする多数の第三者部品を内包しており、
その告知全文は
[`licenses/skiasharp-native-THIRD-PARTY-NOTICES.txt`](./licenses/skiasharp-native-THIRD-PARTY-NOTICES.txt)
に収録しています（Skia は BSD-3-Clause、Adobe DNG SDK は独自条項など、MIT 以外を含みます）。

```
Copyright (c) 2015-2016 Xamarin, Inc.
Copyright (c) 2017-2018 Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

## CommunityToolkit.Mvvm

MVVM の `ObservableObject` / `[ObservableProperty]` / `[RelayCommand]` に使用しています。

```
# .NET Community Toolkit

Copyright (c) .NET Foundation and Contributors

All rights reserved.

## MIT License (MIT)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

## Microsoft.Extensions.DependencyInjection / .Abstractions

DI コンテナに使用しています。ライセンスは上記 .NET Runtime と同一の MIT
(Copyright (c) .NET Foundation and Contributors) です。

---

## Hardcodet.NotifyIcon.Wpf

タスクトレイアイコンに使用しています。

```
The MIT License (MIT)

Copyright (c) Philipp Sumi

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## NHotkey / NHotkey.Wpf

グローバルホットキーに使用しています。

Copyright Thomas Levesque — <https://github.com/thomaslevesque/NHotkey>

Licensed under the Apache License, Version 2.0 (the "License"); you may not use
this file except in compliance with the License. You may obtain a copy of the
License at <http://www.apache.org/licenses/LICENSE-2.0>.

Unless required by applicable law or agreed to in writing, software distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
CONDITIONS OF ANY KIND, either express or implied. See the License for the
specific language governing permissions and limitations under the License.

ライセンス全文は [`licenses/Apache-2.0.txt`](./licenses/Apache-2.0.txt) に収録しています。
上流リポジトリは `NOTICE` ファイルを提供していないため、Apache-2.0 §4(d) の NOTICE 再頒布義務は
発生しません。本プロジェクトは NHotkey に一切の変更を加えていません。
