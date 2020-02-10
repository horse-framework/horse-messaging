import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChannelsComponent } from './channels.component';
import { RouterModule } from '@angular/router';

@NgModule({
    declarations: [ChannelsComponent],
    imports: [
        CommonModule,
        RouterModule.forChild([{ path: '', component: ChannelsComponent }])
    ]
})
export class ChannelsModule { }
