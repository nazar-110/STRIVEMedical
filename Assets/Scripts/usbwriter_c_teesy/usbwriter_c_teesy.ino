void setup() {
  // Initialize USB Serial at any speed (Teensy ignores baud rate and runs at 480Mbps)
  Serial.begin(9600); 
  
  // Wait for the PC to actually open the serial port
  while (!Serial) {
    ; 
  }
}

void loop() {
  // Define the bytes: 1, 2, 3, 4, 5, 6, 7, 8, 9, and 'A' (which is 65 in decimal)
  uint8_t data[] = {1, 2, 3, 4, 5, 6, 7, 8, 9, 'A'};

  // Send the array (10 bytes total)
  Serial.write(data, sizeof(data));

  // Add a delay so you don't flood your PC with data
  delay(1000);
}