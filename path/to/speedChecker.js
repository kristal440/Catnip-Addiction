class SpeedChecker {
    constructor() {
        this.currentSpeed = 0;
        this.verticalSpeed = 0;
        this.isLaying = false;
        this.lastZeroSpeedTime = null;
    }

    updateSpeed(currentSpeed, verticalSpeed) {
        const currentTime = Date.now();
        this.currentSpeed = currentSpeed;
        this.verticalSpeed = verticalSpeed;

        if (currentSpeed === 0 && verticalSpeed === 0) {
            if (this.lastZeroSpeedTime === null) {
                this.lastZeroSpeedTime = currentTime;
            } else if ((currentTime - this.lastZeroSpeedTime) > 3000) {
                this.isLaying = true;
                this.lastZeroSpeedTime = null; // Reset the timer
            }
        } else {
            this.lastZeroSpeedTime = null;
            if (this.isLaying) {
                this.isLaying = false;
            }
        }

        return this.isLaying;
    }
}

// Example usage:
const speedChecker = new SpeedChecker();
console.log(speedChecker.updateSpeed(0, 0)); // false
console.log(speedChecker.updateSpeed(0, 0)); // false
console.log(speedChecker.updateSpeed(0, 0)); // false
console.log(speedChecker.updateSpeed(0, 0)); // true after 3 seconds

console.log(speedChecker.updateSpeed(1, 0)); // false
