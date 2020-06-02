# AutoIt3 Interpreter
This directory contains the "new" .NET5-based AutoIt3 Interpreter.
[TODO : elaborate]


## Extensibility
The AutoIt3 Interpreter is designed to be modular and can be extended in multiple ways:

#### 1) Language packs
If you wish to add a new language pack to the interpreter, create a new JSON file and name it `lang-xx.json`, where `xx` is the two-digit country/language code.
The JSON file must contain the `"meta"`- and `"strings"`-sections as follows:
```json
{
    "meta": {
        "code": "xx",
        "name": "my_language",
        "beta": true
    },
    "strings": {
        /* ... */
    }
}
```
Save the JSON language pack file to the folder `lang/` in order for them to be used by the Interpreter.
The language pack can be used as follows:
```bash
$ autoit3 -l xx [....]
```

A list of all required strings for a complete JSON language pack can be found inside existing language packs.

#### 2) Parser extensions
[TODO]

#### 3) Include resovlers
[TODO]
