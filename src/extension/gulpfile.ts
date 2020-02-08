
import * as gulp from 'gulp';
import { resetPackageVersionPlaceholder, setPackageVersion } from 'nerdbank-gitversioning';

gulp.task('setversion', function() {
    resetPackageVersionPlaceholder();
    return setPackageVersion();
});