import serial
import time

# Configuration
PORT = 'COM7'
BAUD_RATE = 9600 # Teensy ignores this, but the library requires a value

try:
    # Initialize serial connection
    ser = serial.Serial(PORT, BAUD_RATE, timeout=1)
    print(f"--- Connected to {PORT} ---")
    print("--- Press Ctrl+C to stop ---\n")

    while True:
        if ser.in_waiting > 0:
            # Read all bytes currently in the buffer
            raw_data = ser.read(ser.in_waiting)
            
            # Print as a list of integers (e.g., [1, 2, 3, 4, 5, 6, 7, 8, 9, 65])
            print(f"Decimal: {list(raw_data)}")
            
            # Print as Hexadecimal (e.g., 01 02 03 04 05 06 07 08 09 41)
            print(f"Hex:     {raw_data.hex(' ')}")
            print("-" * 30)
            
        time.sleep(0.01)  # Small sleep to prevent high CPU usage

except serial.SerialException as e:
    print(f"Error: Could not open port {PORT}. Is the Teensy plugged in?")
except KeyboardInterrupt:
    print("\nStopping script...")
finally:
    if 'ser' in locals() and ser.is_open:
        ser.close()
        print("Serial port closed.")