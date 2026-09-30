package t;

import android.app.Activity;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.view.WindowManager;

public class Main extends Activity {
    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        if (getIntent().getBooleanExtra("secure", true)) getWindow().setFlags(WindowManager.LayoutParams.FLAG_SECURE, WindowManager.LayoutParams.FLAG_SECURE);
        View v = new View(this);
        v.setBackgroundColor(Color.RED);
        setContentView(v);
    }
}
