# Plankton Suite

Plankton is whatever I want it to be. This file will grow over time.

---

## Startup Parameters

Startup parameters are defined declaratively in the application configuration file (`appsettings.json`). Each parameter is described using a key-value structure, where the key is the command-line option name (e.g. `-input`) and the value defines how that option behaves.

At runtime:

* The application reads all available options from configuration
* Command-line arguments are parsed and validated against those options
* Invalid or missing values fall back to defaults and emit warnings
* Passing `-h`, `--help`, or `-help` prints all available options and exits

Only options declared in configuration are recognized.

---

### Option Definition

Each startup parameter maps to a `CliOption` definition with the following fields:

* **Type**
  The option type. Supported values:

    * `flag` – presence-only switch (no values)
    * `bool` – `true` / `false`
    * `int` – integer value
    * `string` – one or more string values
    * `enum` – one value from a predefined list

* **MinArgs / MaxArgs**
  Minimum and maximum number of values the option accepts (applies to `string` and `enum`).

* **Required**
  Whether the option must be present at startup.

* **Default**
  Value used when the option is omitted or invalid.

* **Help**
  Short description shown when `--help` is requested.

* **Values**
  Allowed values for `enum` options.

---

### Example Configuration

```json
"cli-options": {
  "-input": {
    "type": "string",
    "minArgs": 1,
    "help": "One or more input files"
  },
  "-output": {
    "type": "string",
    "minArgs": 1,
    "maxArgs": 1,
    "required": true,
    "help": "Output file"
  },
  "-enable-scheduling": {
    "type": "bool",
    "default": false,
    "help": "Enables scheduling"
  },
  "-mode": {
    "type": "enum",
    "values": ["fast", "safe", "debug"],
    "default": "safe",
    "help": "Execution mode"
  }
}
```

---

### Help Output Example

```text
Available command line options:
-input               One or more input files
-output              Output file
-enable-scheduling   Enables scheduling
-mode                Execution mode
```

---

Startup parameters are validated when the application boots. Invalid option definitions prevent startup; invalid user input is logged and safely ignored using defaults.
