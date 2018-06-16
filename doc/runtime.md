<img alt="AutoIt++ icon" src="images/icon-1024.png" height="200"/>

# Differences between the AutoIt3 and AutoIt+ runtime behaviours
[go back](../readme.md)

The AutoIt3 is fully compatible with the AutoIt++ dialect, meaning that the [official language reference](https://www.autoitscript.com/autoit3/docs/) applies to AutoIt++.
<br/>
This article highlights the most important **differences** between AutoIt3's and AutoIt++'s runtimes. It is therefore divided in the following sections:

TODO

 - `Default` behaviour
 - `#include` resolver
 - multi-dim jagged arrays
 - implicitly initialized variables
 - λ execution and restrictions
 - TCP and UDP accept IPv6
 - regex engine
 - constant calling and indexing, e.g. `3[5]` or `7(5, $b)`


Some aspects of this article explain the behaviour of certain language feautures, so do please refer to [the according document](language.md) for more information.
<br/>
The runtime behaviour was also part of the syntax' design process, so also refer to [the AutoIt++ syntax reference](syntax.md) for more information.

------
