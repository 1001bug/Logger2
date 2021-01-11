# Logger2
C# async logger in one file. 
- On init create write thread. 
- On log() copy args and format string, put record in List and return.
- write thread pickup entire List and substitute it with empty one, then write picked to file
- not all variables can be copyed (copy args of log()). at this poit can be `.ToString()` used

# Example

```C#
using Logger;

public static Logger2 csv_output;
public static Logger2 logger;

csv_output = new Logger2("log", extension: ".csv",  append: false, encoding: 65001);
logger = new Logger2("log",  append: false, encoding: 65001);



logger.log("send {0} message DUMP: {1}", ORDERS[current_order].is_trade_msg ? "trade" : "nontrade", ORDERS[current_order].M.ToString());


logger.stop();
csv_output.stop();
```
